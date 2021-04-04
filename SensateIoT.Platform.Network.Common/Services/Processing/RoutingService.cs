﻿/*
 * Routing service.
 *
 * @author Michel Megens
 * @email  michel@michelmegens.net
 */

using System;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Google.Protobuf;
using Newtonsoft.Json;
using Prometheus;

using SensateIoT.Platform.Network.Common.Caching.Abstract;
using SensateIoT.Platform.Network.Common.Collections.Abstract;
using SensateIoT.Platform.Network.Common.Services.Background;
using SensateIoT.Platform.Network.Common.Settings;
using SensateIoT.Platform.Network.Contracts.DTO;
using SensateIoT.Platform.Network.Data.Abstract;
using SensateIoT.Platform.Network.Data.DTO;

using ControlMessage = SensateIoT.Platform.Network.Data.DTO.ControlMessage;

namespace SensateIoT.Platform.Network.Common.Services.Processing
{
	public class RoutingService : BackgroundService
	{
		/*
		 * Route messages through the platform:
		 *
		 *		1 Check validity;
		 *		2 Trigger routing;
		 *		3 Live data routing;
		 *		4 Forward to storage.
		 */

		private readonly IRoutingCache m_cache;
		private readonly IMessageQueue m_messages;
		private readonly IInternalRemoteQueue m_internalRemote;
		private readonly IRemoteStorageQueue m_storageQueue;
		private readonly IRemoteNetworkEventQueue m_eventsQueue;
		private readonly IAuthorizationService m_authService;
		private readonly IPublicRemoteQueue m_publicRemote;
		private readonly ILogger<RoutingService> m_logger;
		private readonly RoutingPublishSettings m_settings;
		private readonly Counter m_dropCounter;
		private readonly Counter m_counter;

		private const int DequeueCount = 1000;
		private const string FormatNeedle = "$id";

		public RoutingService(IRoutingCache cache,
							  IMessageQueue queue,
							  IInternalRemoteQueue internalRemote,
							  IPublicRemoteQueue publicRemote,
							  IRemoteStorageQueue storage,
							  IRemoteNetworkEventQueue events,
							  IAuthorizationService auth,
							  IOptions<RoutingPublishSettings> settings,
							  ILogger<RoutingService> logger) : base(logger)
		{
			this.m_settings = settings.Value;
			this.m_messages = queue;
			this.m_cache = cache;
			this.m_eventsQueue = events;
			this.m_internalRemote = internalRemote;
			this.m_publicRemote = publicRemote;
			this.m_authService = auth;
			this.m_logger = logger;
			this.m_storageQueue = storage;

			this.m_dropCounter = Prometheus.Metrics.CreateCounter("router_messages_dropped_total", "Total number of measurements/messages dropped.");
			this.m_counter = Prometheus.Metrics.CreateCounter("router_messages_routed_total", "Total number of measurements/messages routed.");
		}

		protected override async Task ExecuteAsync(CancellationToken token)
		{
			do {
				if(this.m_messages.Count <= 0) {
					try {
						await Task.Delay(this.m_settings.InternalInterval, token);
					} catch(OperationCanceledException) {
						this.m_logger.LogWarning("Routing task cancelled.");
					}

					continue;
				}

				var messages = this.m_messages.DequeueRange(DequeueCount).ToList();
				messages = messages.OrderBy(x => x.SensorID).ToList();

				this.m_logger.LogInformation("Routing {count} messages.", messages.Count);

				var result = Parallel.ForEach(messages, this.Process);

				if(!result.IsCompleted) {
					this.m_logger.LogWarning("Unable to complete routing messages! Break called at iteration: {iteration}.", result.LowestBreakIteration);
				}
			} while(!token.IsCancellationRequested);
		}

		private void Process(IPlatformMessage message)
		{
			try {
				var sensor = this.m_cache[message.SensorID];

				if(sensor == null) {
					this.m_dropCounter.Inc();
					this.m_logger.LogDebug("Dropped message for sensor {sensorId} due to invalid sensor or account.",
										   message.SensorID.ToString());
					return;
				}

				var evt = this.RouteMessage(message, sensor);
				this.m_counter.Inc();
				this.m_eventsQueue.EnqueueEvent(evt);
			} catch(ArgumentException ex) {
				this.m_logger.LogWarning(ex, "Unable to process message!");
			}
		}

		private NetworkEvent RouteMessage(IPlatformMessage message, Sensor sensor)
		{
			var evt = new NetworkEvent {
				SensorID = ByteString.CopyFrom(sensor.ID.ToByteArray()),
				AccountID = ByteString.CopyFrom(sensor.AccountID.ToByteArray())
			};
			evt.Actions.Add(NetworkEventType.MessageRouted);

			if(message.Type == MessageType.ControlMessage) {
				this.RouteControlMessage(message as ControlMessage, sensor);
				evt.MessageType = NetworkMessageType.ControlMessage;
			} else {
				evt.MessageType = message.Type == MessageType.Measurement
					? NetworkMessageType.Measurement
					: NetworkMessageType.Message;
				message.PlatformTimestamp = DateTime.UtcNow;

				if(sensor.StorageEnabled) {
					this.m_storageQueue.Enqueue(message);
					evt.Actions.Add(NetworkEventType.MessageStorage);
				}

				this.MatchTrigger(sensor, message, evt);

				if(sensor.LiveDataRouting == null || sensor.LiveDataRouting?.Count <= 0) {
					return evt;
				}

				evt.Actions.Add(NetworkEventType.MessageLiveData);

				foreach(var info in sensor.LiveDataRouting) {
					this.m_logger.LogDebug("Routing message to live data client: {clientId}.", info.Target);
					this.EnqueueTo(message, info);
				}
			}

			return evt;
		}

		private void MatchTrigger(Sensor sensor, IPlatformMessage message, NetworkEvent evt)
		{
			var textTriggered = false;
			var measurementTriggered = false;

			if(sensor.TriggerInformation == null || sensor.TriggerInformation.Count <= 0) {
				return;
			}

			foreach(var info in sensor.TriggerInformation) {
				if(info.HasActions) {
					evt.Actions.Add(NetworkEventType.MessageTriggered);

					if(!textTriggered && info.IsTextTrigger) {
						textTriggered = true;
						this.EnqueueToTriggerService(message, info.IsTextTrigger);
					} else if(!measurementTriggered && !info.IsTextTrigger) {
						measurementTriggered = true;
						this.EnqueueToTriggerService(message, info.IsTextTrigger);
					}
				}

				if(textTriggered && measurementTriggered) {
					break;
				}
			}
		}

		private void RouteControlMessage(ControlMessage message, Sensor sensor)
		{
			/*
			 * 1. Timestamp the CM;
			 * 2. Sign the control message;
			 * 3. Queue to the correct output queue.
			 */

			var data = JsonConvert.SerializeObject(message, Formatting.None);
			message.Timestamp = DateTime.UtcNow;
			message.Secret = sensor.SensorKey;
			this.m_authService.SignControlMessage(message, data);


			if(message.Destination == ControlMessageType.Mqtt) {
				data = JsonConvert.SerializeObject(message);
				this.m_publicRemote.Enqueue(data, this.m_settings.ActuatorTopicFormat.Replace(FormatNeedle, sensor.ID.ToString()));
				this.m_logger.LogDebug("Publishing control message: {message}", data);
			} else {
				if(sensor.LiveDataRouting == null || sensor.LiveDataRouting?.Count <= 0) {
					return;
				}

				foreach(var info in sensor.LiveDataRouting) {
					this.m_logger.LogDebug("Routing message to live data client: {clientId}.", info.Target);
					this.EnqueueTo(message, info);
				}
			}
		}

		private void EnqueueToTriggerService(IPlatformMessage message, bool isText)
		{
			switch(message.Type) {
			case MessageType.Measurement when isText:
				return;

			case MessageType.Measurement:
				this.m_internalRemote.EnqueueMeasurementToTriggerService(message);
				break;

			case MessageType.Message:
				this.m_internalRemote.EnqueueToMessageTriggerService(message);
				break;

			default:
				this.m_logger.LogError("Received invalid message type. Unable to route to trigger service. " +
									   "The received type is: {type}", message.Type);
				throw new ArgumentException($"Unable to enqueue message of type {message.Type}.");
			}
		}

		private void EnqueueTo(IPlatformMessage message, RoutingTarget target)
		{
			switch(message.Type) {
			case MessageType.ControlMessage:
				this.m_internalRemote.EnqueueControlMessageToTarget(message, target);
				break;
			case MessageType.Message:
				this.m_internalRemote.EnqueueMessageToTarget(message, target);
				break;

			case MessageType.Measurement:
				this.m_internalRemote.EnqueueMeasurementToTarget(message, target);
				break;

			default:
				this.m_logger.LogError("Received invalid message type. Unable to route to live data service. " +
									   "The received type is: {type}", message.Type);
				throw new ArgumentException($"Unable to enqueue message of type {message.Type}.");
			}
		}
	}
}
