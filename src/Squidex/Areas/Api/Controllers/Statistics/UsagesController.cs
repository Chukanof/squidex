﻿// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschränkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Squidex.Areas.Api.Controllers.Statistics.Models;
using Squidex.Domain.Apps.Entities.Apps.Services;
using Squidex.Domain.Apps.Entities.Assets;
using Squidex.Infrastructure.Commands;
using Squidex.Infrastructure.UsageTracking;
using Squidex.Pipeline;
using Squidex.Shared;

namespace Squidex.Areas.Api.Controllers.Statistics
{
    /// <summary>
    /// Retrieves usage information for apps.
    /// </summary>
    [ApiExplorerSettings(GroupName = nameof(Statistics))]
    public sealed class UsagesController : ApiController
    {
        private readonly IUsageTracker usageTracker;
        private readonly IAppPlansProvider appPlanProvider;
        private readonly IAssetUsageTracker assetStatsRepository;

        public UsagesController(
            ICommandBus commandBus,
            IUsageTracker usageTracker,
            IAppPlansProvider appPlanProvider,
            IAssetUsageTracker assetStatsRepository)
            : base(commandBus)
        {
            this.usageTracker = usageTracker;

            this.appPlanProvider = appPlanProvider;
            this.assetStatsRepository = assetStatsRepository;
        }

        /// <summary>
        /// Get api calls for this month.
        /// </summary>
        /// <param name="app">The name of the app.</param>
        /// <returns>
        /// 200 => Usage tracking results returned.
        /// 404 => App not found.
        /// </returns>
        [HttpGet]
        [Route("apps/{app}/usages/calls/month/")]
        [ProducesResponseType(typeof(CurrentCallsDto), 200)]
        [ApiPermission(Permissions.AppCommon)]
        [ApiCosts(0)]
        public async Task<IActionResult> GetMonthlyCalls(string app)
        {
            var count = await usageTracker.GetMonthlyCallsAsync(AppId.ToString(), DateTime.Today);

            var plan = appPlanProvider.GetPlanForApp(App);

            var response = new CurrentCallsDto { Count = count, MaxAllowed = plan.MaxApiCalls };

            return Ok(response);
        }

        /// <summary>
        /// Get api calls in date range.
        /// </summary>
        /// <param name="app">The name of the app.</param>
        /// <param name="fromDate">The from date.</param>
        /// <param name="toDate">The to date.</param>
        /// <returns>
        /// 200 => API call returned.
        /// 404 => App not found.
        /// 400 => Range between from date and to date is not valid or has more than 100 days.
        /// </returns>
        [HttpGet]
        [Route("apps/{app}/usages/calls/{fromDate}/{toDate}/")]
        [ProducesResponseType(typeof(Dictionary<string, CallsUsageDto[]>), 200)]
        [ApiPermission(Permissions.AppCommon)]
        [ApiCosts(0)]
        public async Task<IActionResult> GetUsages(string app, DateTime fromDate, DateTime toDate)
        {
            if (fromDate > toDate && (toDate - fromDate).TotalDays > 100)
            {
                return BadRequest();
            }

            var entities = await usageTracker.QueryAsync(AppId.ToString(), fromDate.Date, toDate.Date);

            var response = entities.ToDictionary(x => x.Key, x => x.Value.Select(CallsUsageDto.FromUsage).ToArray());

            return Ok(response);
        }

        /// <summary>
        /// Get total asset size.
        /// </summary>
        /// <param name="app">The name of the app.</param>
        /// <returns>
        /// 200 => Storage usage returned.
        /// 404 => App not found.
        /// </returns>
        [HttpGet]
        [Route("apps/{app}/usages/storage/today/")]
        [ProducesResponseType(typeof(CurrentStorageDto), 200)]
        [ApiPermission(Permissions.AppCommon)]
        [ApiCosts(0)]
        public async Task<IActionResult> GetCurrentStorageSize(string app)
        {
            var size = await assetStatsRepository.GetTotalSizeAsync(AppId);

            var plan = appPlanProvider.GetPlanForApp(App);

            var response = new CurrentStorageDto { Size = size, MaxAllowed = plan.MaxAssetSize };

            return Ok(response);
        }

        /// <summary>
        /// Get asset usage by date.
        /// </summary>
        /// <param name="app">The name of the app.</param>
        /// <param name="fromDate">The from date.</param>
        /// <param name="toDate">The to date.</param>
        /// <returns>
        /// 200 => Storage usage returned.
        /// 400 => Range between from date and to date is not valid or has more than 100 days.
        /// 404 => App not found.
        /// </returns>
        [HttpGet]
        [Route("apps/{app}/usages/storage/{fromDate}/{toDate}/")]
        [ProducesResponseType(typeof(StorageUsageDto[]), 200)]
        [ApiPermission(Permissions.AppCommon)]
        [ApiCosts(0)]
        public async Task<IActionResult> GetStorageSizes(string app, DateTime fromDate, DateTime toDate)
        {
            if (fromDate > toDate && (toDate - fromDate).TotalDays > 100)
            {
                return BadRequest();
            }

            var entities = await assetStatsRepository.QueryAsync(AppId, fromDate.Date, toDate.Date);

            var models = entities.Select(StorageUsageDto.FromStats).ToArray();

            return Ok(models);
        }
    }
}
