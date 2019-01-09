﻿// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschränkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using System;
using Squidex.Domain.Apps.Core.HandleRules.EnrichedEvents;
using Squidex.Domain.Apps.Core.Rules;
using Squidex.Infrastructure.EventSourcing;

namespace Squidex.Domain.Apps.Core.HandleRules
{
    public interface IRuleTriggerHandler
    {
        Type TriggerType { get; }

        bool Triggers(EnrichedEvent @event, RuleTrigger trigger);

        bool Triggers(IEvent @event, RuleTrigger trigger);
    }
}
