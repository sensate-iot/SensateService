﻿/*
 * Data authorization service.
 *
 * @author Michel Megens
 * @email  michel@michelmegens.net
 */

using System;
using System.Diagnostics;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using SensateService.Helpers;
using SensateService.Infrastructure.Authorization;
using SensateService.Services.Settings;

namespace SensateService.Services.Processing
{
	public class DataAuthorizationService : TimedBackgroundService
	{
		private const int Interval = 1000;
		private const int StartDelay = 1000;

		private readonly IAuthorizationCache m_cache;
		private readonly ILogger<DataAuthorizationService> m_logger;
		private readonly TimeSpan m_reloadInterval;
		private DateTimeOffset m_reloadExpiry;

		public DataAuthorizationService(IAuthorizationCache cache, ILogger<DataAuthorizationService> logger)
		{
			this.m_cache = cache;
			this.m_logger = logger;
			this.m_reloadExpiry = DateTimeOffset.MinValue;
			this.m_reloadInterval = TimeSpan.FromMinutes(5);
		}

		protected override async Task ProcessAsync()
		{
			Stopwatch sw;
			long count;

			sw = Stopwatch.StartNew();
			this.m_logger.LogDebug("Authorization service triggered!");
			count = 0L;

			try {
				if(DateTimeOffset.UtcNow > this.m_reloadExpiry) {
					this.m_logger.LogInformation("Reloading caches.");
					this.m_reloadExpiry = DateTimeOffset.UtcNow.Add(this.m_reloadInterval);
					await this.m_cache.Load().AwaitBackground();
				}

				count = this.m_cache.Process();
			} catch(Exception ex) {
				this.m_logger.LogInformation(ex, $"Authorization cache failed: {ex.InnerException?.Message}");
			}

			sw.Stop();

			if(count > 0) {
				this.m_logger.LogInformation("Number of messages authorized: {count}" + Environment.NewLine +
											 "Processing took {duration}ms.", count, sw.ElapsedMilliseconds);
			}
		}

		protected override void Configure(TimedBackgroundServiceSettings settings)
		{
			settings.Interval = Interval;
			settings.StartDelay = StartDelay;
		}
	}
}