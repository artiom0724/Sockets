﻿using ClientSocket.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace ChatSockets.Models
{
	public static class IpUtils
	{
		private static IPInterfaceProperties InterfaceProperties
		{
			get
			{
				var interfaceData = NetworkInterface.GetAllNetworkInterfaces();
				foreach (var test in interfaceData)
				{
					if (test != null && test.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 && test.OperationalStatus == OperationalStatus.Up)
					{
						return test.GetIPProperties();
					}
				}
				return null;
			}
		}

		private static UnicastIPAddressInformation AddressInformation =>
			InterfaceProperties.UnicastAddresses.FirstOrDefault(uai =>
				uai.Address.AddressFamily == AddressFamily.InterNetwork);

		public static IPAddress LocalAddress => AddressInformation?.Address;

		public static IPAddress SubnetMask => AddressInformation?.IPv4Mask;

		public static IPAddress BroadcastAddress => LocalAddress?.GetBroadcastAddress(SubnetMask);

		public static List<IPAddress> MulticastAddresses => InterfaceProperties.MulticastAddresses
			.Where(ma => ma.Address.AddressFamily == AddressFamily.InterNetwork).Select(ma => ma.Address).ToList();
	}
}
