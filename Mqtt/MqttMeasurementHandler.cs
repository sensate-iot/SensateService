/*
 * MQTT message handler.
 *
 * @author Michel Megens
 * @email  dev@bietje.net
 */

using System;
using System.Diagnostics;
using System.Threading.Tasks;

using Newtonsoft.Json.Linq;

using SensateService.Infrastructure.Repositories;
using SensateService.Middleware;
using SensateService.Models;

namespace SensateService.Mqtt
{
	public class MqttMeasurementHandler : MqttHandler
	{
		private readonly ISensorRepository sensors;
		private readonly IMeasurementRepository measurements;

		public MqttMeasurementHandler(ISensorRepository sensors, IMeasurementRepository measurements)
		{
			this.sensors = sensors;
			this.measurements = measurements;
		}

		public override void OnMessage(string topic, string msg)
		{
			throw new System.NotImplementedException();
		}

		public override async Task OnMessageAsync(string topic, string message)
		{
			Sensor sensor;
			string id;
			JObject obj;

			try {
				obj = JObject.Parse(message);
				id = obj.GetValue("CreatedById").Value<string>();

				if(id == null)
					return;

				sensor = await this.sensors.GetAsync(id);
				await this.measurements.ReceiveMeasurement(sensor, obj as JObject);
			} catch(Exception ex) {
				Debug.WriteLine($"Error: {ex.Message}");
				Debug.WriteLine($"Received a buggy MQTT message: {message}");
			}
		}
	}
}
