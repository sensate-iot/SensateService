/*
 * MQTT service init.
 *
 * @author: Michel Megens
 * @email:  michel.megens@sonatolabs.com
 */

using System;
using System.Reflection;
using System.Linq;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using SensateService.Middleware;
using SensateService.Services;
using System.Collections.Generic;
using SensateService.Services.Processing;
using SensateService.Services.Settings;

namespace SensateService.Init
{
	public static class MqttServiceInitExtensions
	{
		public static IServiceCollection AddMqttService(this IServiceCollection service, Action<MqttServiceOptions> setup)
		{
			service.AddSingleton<IHostedService, MqttService>();

			if(setup != null)
				service.Configure<MqttServiceOptions>(setup);

			foreach(var etype in Assembly.GetEntryAssembly().ExportedTypes) {
				if(etype.GetTypeInfo().BaseType == typeof(MqttHandler))
					service.AddScoped(etype);
			}

			return service;
		}

		public static IServiceCollection AddInternalMqttService(this IServiceCollection services, Action<InternalMqttServiceOptions> setup)
		{
			if(setup != null)
				services.Configure(setup);

			services.AddSingleton<IHostedService, InternalMqttService>();

			services.AddSingleton(provider => {
				var s = provider.GetServices<IHostedService>().ToList();
				var mqservice = s.Find(x => x.GetType() == typeof(InternalMqttService)) as IMqttPublishService;
				return mqservice;
			});

			return services;
		}

		public static void MapMqttTopic<T>(this IServiceProvider sp, string topic) where T : MqttHandler
		{
			MqttService mqtt;
			List<IHostedService> services;

			/*
			 * If anybody knows a cleaner way of going about
			 * IHostedServices: do let me know.
			 *
			 * For now we just get *every* IHostedService and find the one
			 * we need. I'm truly sorry you are a witness to this savage
			 * piece of SWE.
			 */
			services = sp.GetServices<IHostedService>().ToList();
			mqtt = services.Find(x => x.GetType() == typeof(MqttService)) as MqttService;
			mqtt?.MapTopicHandler<T>(topic);
		}
	}
}
