﻿/*
 * DataCache reload settings.
 *
 * @author Michel Megens
 * @email  michel@michelmegens.net
 */

using System;

namespace SensateIoT.Platform.Network.Common.Settings
{
	public class DataReloadSettings
	{
		public TimeSpan DataReloadInterval { get; set; }
		public TimeSpan StartDelay { get; set; }
		public TimeSpan TimeoutScanInterval { get; set; }
		public TimeSpan LiveDataReloadInterval { get; set; }
	}
}
