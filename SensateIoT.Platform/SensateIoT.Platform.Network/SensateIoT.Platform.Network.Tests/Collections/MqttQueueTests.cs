﻿/*
 * MQTT queue unit tests.
 *
 * @author Michel Megens
 * @email  michel@michelmegens.net
 */

using System;
using System.Collections.Generic;

using Microsoft.Extensions.Options;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using MongoDB.Bson;

using SensateIoT.Platform.Network.Common.Collections.Remote;
using SensateIoT.Platform.Network.Common.Settings;
using SensateIoT.Platform.Network.Data.DTO;
using SensateIoT.Platform.Network.Data.Models;
using SensateIoT.Platform.Network.Tests.Utility;

namespace SensateIoT.Platform.Network.Tests.Collections
{
	[TestClass]
	public class MqttQueueTests
	{
		[TestMethod]
		public void CanRouteLiveDataMessages()
		{
			var queue = BuildRemoteQueue();

			queue.EnqueueMessageToTarget(new Message {
				Timestamp = DateTime.UtcNow,
				PlatformTimestamp = DateTime.UtcNow,
				Data = "Hello, World",
				SensorId = ObjectId.GenerateNewId()
			}, new RoutingTarget {
				Target = "Local"
			});

			queue.EnqueueMessageToTarget(new Message {
				Timestamp = DateTime.UtcNow,
				PlatformTimestamp = DateTime.UtcNow,
				Data = "Hello, World",
				SensorId = ObjectId.GenerateNewId()
			}, new RoutingTarget {
				Target = "Remote"
			});

			queue.FlushLiveDataAsync();
			Assert.AreEqual(1, ClientStub.GetPublishCount("sensateiot/internal/messages/Local/bulk"));
			Assert.AreEqual(1, ClientStub.GetPublishCount("sensateiot/internal/messages/Remote/bulk"));
		}

		[TestMethod]
		public void CanRouteLiveDataMeasurements()
		{
			var queue = BuildRemoteQueue();

			queue.EnqueueMeasurementToTarget(new Measurement {
				Timestamp = DateTime.UtcNow,
				PlatformTimestamp = DateTime.UtcNow,
				Latitude = 1.1234M,
				Longitude = 1.134643M,
				SensorId = ObjectId.GenerateNewId(),
				Data = new Dictionary<string, DataPoint>()
			}, new RoutingTarget() { Target = "Local" });

			queue.EnqueueMeasurementToTarget(new Measurement {
				Timestamp = DateTime.UtcNow,
				PlatformTimestamp = DateTime.UtcNow,
				Latitude = 1.1234M,
				Longitude = 1.134643M,
				SensorId = ObjectId.GenerateNewId(),
				Data = new Dictionary<string, DataPoint>()
			}, new RoutingTarget() { Target = "Local" });

			queue.FlushLiveDataAsync();
			Assert.AreEqual(1, ClientStub.GetPublishCount("sensateiot/internal/measurements/Local/bulk"));
		}

		internal static MqttClientStub ClientStub = new MqttClientStub();

		internal static IRemoteQueue BuildRemoteQueue()
		{
			var settings = new QueueSettings {
				LiveDataQueueTemplate = "sensateiot/internal/$type/$target/bulk",
				TriggerQueueTemplate = "sensateiot/internal/$type/bulk"
			};


			var remote = new InternalMqttQueue(new OptionsWrapper<QueueSettings>(settings), ClientStub);

			remote.SyncLiveDataHandlers(new[] {
				new LiveDataHandler {
					Enabled = true,
					Name = "Local"
				},
				new LiveDataHandler {
					Enabled = true,
					Name = "Remote"
				}
			});

			return remote;
		}
	}
}
