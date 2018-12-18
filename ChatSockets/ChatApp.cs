using ChatSockets.Models;
using ClientSocket.Helpers;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using static System.Linq.Enumerable;
using System.Threading;

namespace ChatSockets
{
	public class ChatApp
	{
		private readonly Socket _socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

		private readonly StringBuilder _buffer = new StringBuilder();

		private static string SymbolsLine(int offset = 0, string symbol = " ") =>
			string.Join("", Range(0, Console.WindowWidth - 1 - offset).Select(_ => symbol));

		private IPAddress _currentMulticastGroup;

		public void Start()
		{
			ShowNetworkInformation();

			new Thread(ReceiveMessage).Start();
			HandleUserInput();
		}

		private static void ShowNetworkInformation()
		{
			Console.WriteLine($"Local: {IpUtils.LocalAddress} \t Broadcast: {IpUtils.BroadcastAddress} \t Mask: {IpUtils.SubnetMask} \t Multicast: {string.Join(", ", IpUtils.MulticastAddresses)}");
			Console.WriteLine($"Not use <connect > and <disconnect> in start messages. Start chat!\n");
		}

		private void HandleUserInput()
		{
			while (true)
			{
				if (Console.KeyAvailable)
				{
					var key = Console.ReadKey(false);
					_buffer.Append(key.KeyChar);

					if (key.Key == ConsoleKey.Enter)
					{
						ParseUserInput(_buffer.ToString());
						_buffer.Clear();
						Console.Write($"{SymbolsLine()}\r");
					}
					else if (key.Key == ConsoleKey.Backspace)
					{
						_buffer.Remove(_buffer.Length - 1 - (_buffer.Length > 1 ? 1 : 0),
							1 + (_buffer.Length > 1 ? 1 : 0));
						Console.Write($"\r{SymbolsLine()}\r{_buffer}");
					}
				}
			}
		}

		private void ParseUserInput(string input)
		{
			if (input.StartsWith("connect "))
			{
				input = input.Replace("connect ", string.Empty).TrimEnd();
				if (IPAddress.TryParse(input, out _currentMulticastGroup))
				{
					_socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.MulticastTimeToLive, 10);
					_socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.AddMembership,
						new MulticastOption(_currentMulticastGroup));
				}
				else
				{
					ShowMessage("Wrong address.");
				}
			}
			else if (input.StartsWith("disconnect"))
			{
				_socket.SetSocketOption(SocketOptionLevel.IP, SocketOptionName.DropMembership,
					new MulticastOption(_currentMulticastGroup));
				_currentMulticastGroup = null;
			}
			else
			{
				_socket.SendTo(input.StringToByteArray(),
					new IPEndPoint(_currentMulticastGroup ?? IpUtils.BroadcastAddress, 27000));
			}
		}

		private void ReceiveMessage()
		{
			EndPoint endPoint = new IPEndPoint(IPAddress.Any, 27000);
			try
			{
				_socket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.Broadcast, true);
				_socket.Bind(endPoint);
			}catch(Exception exc)
			{
				Console.WriteLine("It's your machine. Run app on unother machine");
				return;
			}
			while (true)
			{
				if (_socket.Available > 0)
				{
					var buf = new byte[_socket.Available];
					endPoint = new IPEndPoint(IPAddress.Any, 27000);
					_socket.ReceiveFrom(buf, ref endPoint);
					ShowMessage($"{((IPEndPoint)endPoint).Address}-user say: {buf.ByteArrayToString().TrimEnd()}");
				}
			}
		}

		private void ShowMessage(string message)
		{
			Console.WriteLine($"\r{message}");
			Console.Write(_buffer);
		}
	}
}
