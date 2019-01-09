﻿// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschränkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NodaTime;
using Squidex.Domain.Apps.Core.Rules;
using Squidex.Domain.Apps.Events;
using Squidex.Infrastructure;
using Squidex.Infrastructure.EventSourcing;
using Squidex.Infrastructure.Json;

namespace Squidex.Domain.Apps.Core.HandleRules
{
    public class RuleService
    {
        private readonly Dictionary<Type, IRuleActionHandler> ruleActionHandlers;
        private readonly Dictionary<Type, IRuleTriggerHandler> ruleTriggerHandlers;
        private readonly TypeNameRegistry typeNameRegistry;
        private readonly IEventEnricher eventEnricher;
        private readonly IJsonSerializer jsonSerializer;
        private readonly IClock clock;

        public RuleService(
            IEnumerable<IRuleTriggerHandler> ruleTriggerHandlers,
            IEnumerable<IRuleActionHandler> ruleActionHandlers,
            IEventEnricher eventEnricher,
            IJsonSerializer jsonSerializer,
            IClock clock,
            TypeNameRegistry typeNameRegistry)
        {
            Guard.NotNull(jsonSerializer, nameof(jsonSerializer));
            Guard.NotNull(ruleTriggerHandlers, nameof(ruleTriggerHandlers));
            Guard.NotNull(ruleActionHandlers, nameof(ruleActionHandlers));
            Guard.NotNull(typeNameRegistry, nameof(typeNameRegistry));
            Guard.NotNull(eventEnricher, nameof(eventEnricher));
            Guard.NotNull(clock, nameof(clock));

            this.typeNameRegistry = typeNameRegistry;

            this.ruleTriggerHandlers = ruleTriggerHandlers.ToDictionary(x => x.TriggerType);
            this.ruleActionHandlers = ruleActionHandlers.ToDictionary(x => x.ActionType);

            this.eventEnricher = eventEnricher;

            this.jsonSerializer = jsonSerializer;

            this.clock = clock;
        }

        public virtual async Task<RuleJob> CreateJobAsync(Rule rule, Envelope<IEvent> @event)
        {
            Guard.NotNull(rule, nameof(rule));
            Guard.NotNull(@event, nameof(@event));

            if (!rule.IsEnabled)
            {
                return null;
            }

            if (!(@event.Payload is AppEvent appEvent))
            {
                return null;
            }

            var actionType = rule.Action.GetType();

            if (!ruleTriggerHandlers.TryGetValue(rule.Trigger.GetType(), out var triggerHandler))
            {
                return null;
            }

            if (!ruleActionHandlers.TryGetValue(actionType, out var actionHandler))
            {
                return null;
            }

            var appEventEnvelope = @event.To<AppEvent>();

            var enrichedEvent = await eventEnricher.EnrichAsync(appEventEnvelope);

            if (!triggerHandler.Triggers(enrichedEvent, rule.Trigger))
            {
                return null;
            }

            var now = clock.GetCurrentInstant();

            var eventTime =
                @event.Headers.ContainsKey(CommonHeaders.Timestamp) ?
                @event.Headers.Timestamp() :
                now;

            var expires = eventTime.Plus(Constants.ExpirationTime);

            if (expires < now)
            {
                return null;
            }

            var actionName = typeNameRegistry.GetName(actionType);
            var actionData = await actionHandler.CreateJobAsync(enrichedEvent, rule.Action);

            var json = jsonSerializer.Serialize(actionData.Data);

            enrichedEvent.CalculatePartition();

            var job = new RuleJob
            {
                JobId = Guid.NewGuid(),
                ActionName = actionName,
                ActionData = json,
                AppId = appEvent.AppId.Id,
                Created = now,
                EventName = enrichedEvent.Name,
                ExecutionPartition = enrichedEvent.Partition,
                Expires = expires,
                Description = actionData.Description
            };

            return job;
        }

        public virtual async Task<(string Dump, RuleResult Result, TimeSpan Elapsed)> InvokeAsync(string actionName, string job)
        {
            try
            {
                var actionType = typeNameRegistry.GetType(actionName);
                var actionWatch = Stopwatch.StartNew();

                var actionHandler = ruleActionHandlers[actionType];

                var deserialized = jsonSerializer.Deserialize<object>(job, actionHandler.DataType);

                var result = await actionHandler.ExecuteJobAsync(deserialized);

                actionWatch.Stop();

                var dumpBuilder = new StringBuilder(result.Dump);

                dumpBuilder.AppendLine();
                dumpBuilder.AppendFormat("Elapsed {0}.", actionWatch.Elapsed);
                dumpBuilder.AppendLine();

                if (result.Exception is TimeoutException || result.Exception is OperationCanceledException)
                {
                    dumpBuilder.AppendLine();
                    dumpBuilder.AppendLine("Action timed out.");

                    return (dumpBuilder.ToString(), RuleResult.Timeout, actionWatch.Elapsed);
                }
                else if (result.Exception != null)
                {
                    return (dumpBuilder.ToString(), RuleResult.Failed, actionWatch.Elapsed);
                }
                else
                {
                    return (dumpBuilder.ToString(), RuleResult.Success, actionWatch.Elapsed);
                }
            }
            catch (Exception ex)
            {
                return (ex.ToString(), RuleResult.Failed, TimeSpan.Zero);
            }
        }
    }
}
