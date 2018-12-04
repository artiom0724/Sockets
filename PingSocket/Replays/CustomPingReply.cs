using PingSocket.Models;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;

namespace PingSocket.Replays
{
	public class CustomPingReply
	{
		public IPAddress Address { get; set; }
		public IPStatus Status { get; set; }
		public long ElapsedTime { get; set; }

		public static CustomPingReply Parse(EndPoint endPoint, IcmpPackage icmpResponse, long time)
		{
			return new CustomPingReply
			{
				Address = ((IPEndPoint)endPoint).Address,
				Status = icmpResponse?.Type == IcmpType.EchoResponse ? IPStatus.Success : IPStatus.Unknown,
				ElapsedTime = time
			};
		}
	}
}
