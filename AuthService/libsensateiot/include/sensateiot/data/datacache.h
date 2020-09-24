/*
 * Data caching header.
 *
 * @author Michel Megens
 * @email  michel@michelmegens.net
 */

#pragma once

#include <sensateiot/models/sensor.h>
#include <sensateiot/models/user.h>
#include <sensateiot/models/measurement.h>
#include <sensateiot/models/apikey.h>

#include <sensateiot/cache/memorycache.h>

#include <boost/unordered_set.hpp>
#include <boost/uuid/uuid.hpp>
#include <boost/chrono/chrono.hpp>

#include <vector>
#include <string>
#include <utility>
#include <optional>

namespace sensateiot::data
{
	class DataCache {
	public:
		enum class SensorStatus {
			Available,
			Unavailable,
			Unknown
		};

		typedef boost::chrono::high_resolution_clock::time_point TimePoint;
		typedef std::pair<bool, std::optional<models::Sensor>> SensorLookupType;
		static constexpr long DefaultTimeoutMinutes = 6;

		explicit DataCache(std::chrono::high_resolution_clock::duration timeout);
		explicit DataCache();

		void Append(std::vector<models::Sensor>& sensors);
		void Append(std::vector<models::User>& users);
		void Append(std::vector<models::ApiKey>& keys);
		void AppendBlackList(const models::ObjectId& objId);
		void AppendBlackList(const std::vector<models::ObjectId>& objIds);

		void CleanupFor(boost::chrono::milliseconds millis);
		void Clear();
		void Cleanup();

		void FlushUser(const boost::uuids::uuid& id);
		void FlushSensor(const models::ObjectId& id);
		void FlushKey(const std::string& key);

		/* Found, sensor data */
		std::pair<bool, std::optional<models::Sensor>> GetSensor(const models::ObjectId& id, TimePoint tp) const;
		bool IsBlackListed(const models::ObjectId& objId) const;
		SensorStatus CanProcess(const models::Measurement& raw) const;

	private:
		cache::MemoryCache<models::ObjectId, models::Sensor> m_sensors;
		cache::MemoryCache<boost::uuids::uuid, models::User> m_users;
		cache::MemoryCache<std::string, std::string> m_keys;
		/*stl::Map<models::ObjectId, models::Sensor> m_sensors;
		stl::Map<boost::uuids::uuid, models::User> m_users;
		stl::Set<std::string> m_keys;
		stl::Set<models::ObjectId> m_blackList;*/
	};
}
