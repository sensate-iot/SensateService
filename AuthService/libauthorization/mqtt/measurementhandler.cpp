/*
 * MQTT measurement handler.
 *
 * @author Michel Megens
 * @email  michel@michelmegens.net
 */

#include <sensateiot/measurementhandler.h>

#include <iostream>

namespace sensateiot::mqtt
{
	MeasurementHandler::MeasurementHandler(IMqttClient &client) :
		m_internal(client)
	{

	}

	MeasurementHandler::~MeasurementHandler()
	{
		this->m_lock.lock();
		this->m_measurements.clear();
		this->m_lock.unlock();
	}

	MeasurementHandler &MeasurementHandler::operator=(MeasurementHandler &&rhs) noexcept
	{
		std::scoped_lock l(this->m_lock, rhs.m_lock);

		this->m_internal = std::move(rhs.m_internal);
		this->m_measurements = std::move(rhs.m_measurements);
		return *this;
	}

	MeasurementHandler::MeasurementHandler(MeasurementHandler &&rhs) noexcept
	{
		std::scoped_lock l(this->m_lock, rhs.m_lock);

		this->m_internal = std::move(rhs.m_internal);
		this->m_measurements = std::move(rhs.m_measurements);
	}

	void MeasurementHandler::PushMeasurement(std::string json)
	{
		std::scoped_lock l(this->m_lock);

		std::cout << json;
		this->m_measurements.emplace_back(std::move(json));
	}

	void MeasurementHandler::Process()
	{

	}
}
