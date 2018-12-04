using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;

namespace PingSocket.Replays
{
	public class TracertPingReply
	{
		private long _time;

		public long Time
		{
			get => _time == 0 ? 0 : _time;
			set => _time = value;
		}

		public IPAddress Address { get; set; }
	}
}
