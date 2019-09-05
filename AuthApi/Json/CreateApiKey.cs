﻿/*
 * API key creation request.
 *
 * @author Michel Megens
 * @email  michel.megens@sonatolabs.com
 */

namespace SensateService.AuthApi.Json
{
	public class CreateApiKey
	{
		public string Name { get; set; }
		public bool ReadOnly { get; set; }
	}
}