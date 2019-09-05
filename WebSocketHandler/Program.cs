﻿/*
 * WebSocketHandler entry point.
 *
 * @author Michel Megens
 * @email  michel.megens@sonatolabs.com
 */

using System;
using System.IO;

using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

using SensateService.WebSocketHandler.Application;

namespace SensateService.WebSocketHandler 
{
	public class Program
	{
		public static void Main(string[] args)
		{
			IWebHost wh;

			Console.WriteLine($"Starting {SensateService.Version.VersionString}");
			wh = BuildWebHost(args);
			wh.Run();
		}

		private static IWebHost BuildWebHost(string[] args)
		{
			var conf = new ConfigurationBuilder()
						.SetBasePath(Directory.GetCurrentDirectory())
						.AddJsonFile("hosting.json")
						.Build();

			var wh = WebHost.CreateDefaultBuilder(args)
				.UseConfiguration(conf)
				.UseKestrel()
				.UseContentRoot(Directory.GetCurrentDirectory())
				.ConfigureAppConfiguration((hostingContext, config) => {
					if(hostingContext.HostingEnvironment.IsProduction())
						config.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
					else
						config.AddUserSecrets<ApplicationStarter>();
					config.AddEnvironmentVariables();
				})
				.ConfigureLogging((hostingContext, logging) => {
					logging.AddConfiguration(hostingContext.Configuration.GetSection("Logging"));
					logging.AddConsole();
					logging.AddDebug();
				})
				.UseStartup<ApplicationStarter>();
			return wh.Build();
		}
	}
}
