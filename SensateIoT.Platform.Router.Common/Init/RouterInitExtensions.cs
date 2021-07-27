﻿/*
 * Router init extensions.
 *
 * @author Michel Megens
 * @email  michel@michelmegens.net
 */

using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SensateIoT.Platform.Router.Common.Caching.Abstract;
using SensateIoT.Platform.Router.Common.Collections.Abstract;
using SensateIoT.Platform.Router.Common.Routing;
using SensateIoT.Platform.Router.Common.Routing.Abstract;
using SensateIoT.Platform.Router.Common.Routing.Routers;

namespace SensateIoT.Platform.Router.Common.Init
{
	public static class RouterInitExtensions
	{
		public static void AddMessageRouter(this IServiceCollection collection)
		{
			collection.AddSingleton<BaseRouter>();
			collection.AddSingleton<AuthorizationRouter>();
			collection.AddSingleton<ControlMessageRouter>();
			collection.AddSingleton<LiveDataRouter>();
			collection.AddSingleton<TriggerRouter>();
			collection.AddSingleton<StorageRouter>();

			collection.AddSingleton(provider => {
				var cache = provider.GetRequiredService<IRoutingCache>();
				var queue = provider.GetRequiredService<IRemoteNetworkEventQueue>();
				var logger = provider.GetRequiredService<ILogger<CompositeRouter>>();
				var router = new CompositeRouter(cache, queue, logger) as IMessageRouter;

				AddRouters(router, provider);

				return router;
			});
		}

		private static void AddRouters(IMessageRouter router, IServiceProvider provider)
		{
			var @base = provider.GetRequiredService<BaseRouter>();
			var controlMessages = provider.GetRequiredService<ControlMessageRouter>();
			var triggers = provider.GetRequiredService<TriggerRouter>();
			var liveData = provider.GetRequiredService<LiveDataRouter>();
			var storage = provider.GetRequiredService<StorageRouter>();
			var auth = provider.GetRequiredService<AuthorizationRouter>();

			router.AddRouter(@base);
			router.AddRouter(auth);
			router.AddRouter(controlMessages);
			router.AddRouter(triggers);
			router.AddRouter(liveData);
			router.AddRouter(storage);
		}
	}
}
