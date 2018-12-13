using PingSocket.Models;
using PingSocket.Replays;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace PingSocket.Services
{
	public class TracertSocketSend
	{
		private readonly Socket _socket;
		private readonly Stopwatch _timer = new Stopwatch();

		public TracertSocketSend()
		{
			_socket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp);
			_socket.Bind(new IPEndPoint(IPAddress.Any, 0));
			_socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.IpTimeToLive, 128);
			_socket.ReceiveTimeout = 2000;
		}

		public Dictionary<int, List<TracertPingReply>> Send(string host, int hopsCount)
		{
			var destination = new IPEndPoint(GetAddressByHost(host), 0);

			var icmpPackage = new IcmpPackage { Type = IcmpType.EchoRequest, Payload = Guid.NewGuid().ToString("N") };
			var package = icmpPackage.Build();

			_timer.Restart();
			
			var traceRouteDict = new Dictionary<int, List<TracertPingReply>>();

			var timer = new Stopwatch();

			for (var i = 1; i <= hopsCount; i++)
			{
				_timer.Restart();
				traceRouteDict.Add(i, new List<TracertPingReply>());

				var buffer = new byte[package.Length * 2];
				try
				{
					short ttl = 1;

					int receivedByteCount;
					while (ttl< 100)
					{
						_socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.IpTimeToLive, ttl);

						ttl++;
						_socket.SendTo(package, destination);
						EndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);
						try
						{
							receivedByteCount = _socket.ReceiveFrom(buffer, ref endPoint);
						}
						catch (Exception except)
						{

						}
						//if(((IPEndPoint)endPoint).Address.ToString() != "0.0.0.0" )
						//{
						//}
						traceRouteDict[i].Add(new TracertPingReply() {
							Address = ((IPEndPoint)endPoint).Address,
							Time = _timer.ElapsedMilliseconds
						});

						if (destination.Address.Equals(((IPEndPoint)endPoint).Address)) break;
					}
					_timer.Stop();
				}
				catch (SocketException exc)
				{
					_timer.Stop();
				}

				traceRouteDict[i].Add(new TracertPingReply { Time = timer.ElapsedMilliseconds, Address = destination.Address });
			}
			return traceRouteDict;
		}

		public void Dispose()
		{
			_socket.Close();
		}

		private IPAddress GetAddressByHost(string host) => IPAddress.TryParse(host, out var address)
			? address
			: Dns.GetHostEntry(host).AddressList.FirstOrDefault();
	}
}
