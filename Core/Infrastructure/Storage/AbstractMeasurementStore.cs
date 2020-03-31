﻿/*
 * Abstract measurement store definition. This class implements
 * abstract storage features, such as the validation of received
 * measurements.
 *
 * @author Michel Megens
 * @email  michel@michelmegens.net
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using MongoDB.Driver.GeoJsonObjectModel;
using SensateService.Enums;

using SensateService.Models;
using SensateService.Models.Json.In;

namespace SensateService.Infrastructure.Storage
{
	public abstract class AbstractMeasurementStore : IMeasurementStore
	{
		protected IServiceProvider Provider { get; }
		protected ILogger Logger { get; }

		private const int MaxDatapointLength = 25;

		protected AbstractMeasurementStore(IServiceProvider provider, ILogger logger)
		{
			this.Provider = provider;
			this.Logger = logger;
		}

		protected Measurement ProcessRawMeasurement(Sensor sensor, RawMeasurement obj)
		{
			Measurement measurement;
			IDictionary<string, DataPoint> datapoints;
			GeoJsonPoint<GeoJson2DGeographicCoordinates> point;

			if(!obj.IsCreatedBy(sensor))
				return null;

			if(obj.TryParseData(out var data)) {
				datapoints = data;

				if(datapoints == null)
					return null;

				if(datapoints.Count <= 0 || datapoints.Count >= MaxDatapointLength)
					return null;
			} else {
				return null;
			}

			if(obj.Latitude.HasValue && obj.Longitude.HasValue) {
				var coords = new GeoJson2DGeographicCoordinates(obj.Longitude.Value, obj.Latitude.Value);
				point = new GeoJsonPoint<GeoJson2DGeographicCoordinates>(coords);
			} else {
				point = null;
			}

			measurement = new Measurement {
				Data = datapoints,
				Timestamp = obj.CreatedAt ?? DateTime.Now.ToUniversalTime(),
				Location = point
			};

			return measurement;
		}

		protected bool CanInsert(IEnumerable<string> roles)
		{
			return !roles.Contains(SensateRole.Banned);
		}

		protected bool CanInsert(SensateUser user)
		{
			var banned = SensateRole.Banned.ToUpperInvariant();
			return user.UserRoles.All(role => role.Role.NormalizedName != banned);
		}

		protected bool InsertAllowed(SensateUser user, string key)
		{
			var apikey = user.ApiKeys.FirstOrDefault(k => k.ApiKey == key);

			if(apikey == null)
				return false;

			return !apikey.Revoked && apikey.Type == ApiKeyType.SensorKey;
		}

		public abstract Task StoreAsync(RawMeasurement obj, RequestMethod method);
	}
}
