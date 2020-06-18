/*
 * API key repository.
 *
 * @author Michel Megens
 * @email  michel@michelmegens.net
 */

#include <config/database.h>

#include <sensateiot/services/apikeyrepository.h>
#include <sensateiot/util/log.h>

#include <boost/uuid/uuid.hpp>
#include <boost/uuid/uuid_io.hpp>
#include <boost/lexical_cast.hpp>

#include <string>
#include <vector>

namespace sensateiot::services
{
	ApiKeyRepository::ApiKeyRepository(const config::PostgreSQL &pgsql) :
			AbstractApiKeyRepository(pgsql), m_connection(pgsql.GetConnectionString())
	{
		auto &log = util::Log::GetLog();

		if(this->m_connection.is_open()) {
			log << "API keys: Connected to PostgreSQL!" << util::Log::NewLine;
		} else {
			log << "API keys: Unable to connect to PostgreSQL!" << util::Log::NewLine;
		}
	}

	std::vector<std::string> ApiKeyRepository::GetAllSensorKeys()
	{
		std::string query("SELECT \"ApiKey\"\n"
		                  "FROM \"ApiKeys\"\n"
		                  "WHERE \"Type\" = 0 AND \"Revoked\" = FALSE");

		pqxx::nontransaction q(this->m_connection);
		pqxx::result res(q.exec(query));
		std::vector<std::string> keys;

		for(const auto &row : res) {
			keys.push_back(row[0].as<std::string>());
		}

		return keys;
	}

	std::vector<std::string> ApiKeyRepository::GetKeys(const std::vector<std::string> &ids)
	{
		std::string rv("(");

		for(auto idx = 0U; idx < ids.size(); idx++) {
			rv += '\'' + ids[idx] + '\'';

			if((idx + 1) != ids.size()) {
				rv += ",";
			}
		}

		rv += ")";

		std::string::size_type pos = 0u;
		std::string query(
				"SELECT \"ApiKeys\".\"ApiKey\"\n"
				"FROM \"ApiKeys\"\n"
				"WHERE \"Type\" = 0 AND \"Revoked\" = FALSE AND\n"
				"\"ApiKeys\".\"ApiKey\" IN ?");

		pos = query.find('?', pos);
		query.replace(pos, sizeof(char), rv);

		pqxx::nontransaction q(this->m_connection);
		pqxx::result res(q.exec(query));
		std::vector<std::string> keys;

		for(const auto &row: res) {
			keys.push_back(row[0].as<std::string>());
		}

		return keys;
	}

	std::vector<std::string> ApiKeyRepository::GetKeysByOwners(const boost::unordered_set<boost::uuids::uuid> &ids)
	{
		std::string rv("(");
		auto idx = 0UL;

		for(auto iter = ids.begin(); idx < ids.size(); ++iter, idx++) {
			rv += '\'' + boost::lexical_cast<std::string>(*iter) + '\'';

			if((idx + 1) != ids.size()) {
				rv += ",";
			}
		}

		rv += ")";

		std::string::size_type pos = 0u;
		std::string query(
				"SELECT \"ApiKeys\".\"ApiKey\"\n"
				"FROM \"ApiKeys\"\n"
				"WHERE \"Type\" = 0 AND \"Revoked\" = FALSE AND\n"
				"\"ApiKeys\".\"UserId\" IN ?");

		pos = query.find('?', pos);
		query.replace(pos, sizeof(char), rv);

		pqxx::nontransaction q(this->m_connection);
		pqxx::result res(q.exec(query));
		std::vector<std::string> keys;

		for(const auto &row: res) {
			keys.push_back(row[0].as<std::string>());
		}

		return keys;
	}
}
