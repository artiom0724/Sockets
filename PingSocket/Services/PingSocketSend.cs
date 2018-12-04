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
	public class PingSocketSend : IDisposable
	{
		private readonly Socket _socket;
		private readonly Stopwatch _timer = new Stopwatch();

		public PingSocketSend()
		{
			_socket = new Socket(AddressFamily.InterNetwork, SocketType.Raw, ProtocolType.Icmp);
			_socket.Bind(new IPEndPoint(IPAddress.Any, 0));
			_socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.IpTimeToLive, 128);
			_socket.ReceiveTimeout = 2000;
		}

		public CustomPingReply Send(string host)
		{
			var destination = new IPEndPoint(GetAddressByHost(host), 0);

			var icmpPackage = new IcmpPackage { Type = IcmpType.EchoRequest, Payload = Guid.NewGuid().ToString("N") };
			var package = icmpPackage.Build();

			_timer.Restart();
			_socket.SendTo(package, destination);

			var buffer = new byte[package.Length * 2];
			try
			{
				int receivedByteCount;
				while (true)
				{
					EndPoint endPoint = new IPEndPoint(IPAddress.Any, 0);
					receivedByteCount = _socket.ReceiveFrom(buffer, ref endPoint);

					if (destination.Address.Equals(((IPEndPoint)endPoint).Address)) break;
				}
				_timer.Stop();
				return CustomPingReply.Parse(destination,
					IcmpPackage.RestorePackage(buffer.Take(receivedByteCount).ToArray()), _timer.ElapsedMilliseconds);
			}
			catch (SocketException)
			{
				_timer.Stop();
				return CustomPingReply.Parse(destination, null, _timer.ElapsedMilliseconds);
			}
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
