using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ClientSocket.Models
{
	public class MultiSocketModel
	{
		public Socket handler { get; set; }

		public Socket socketUDP { get; set; }

		public Socket socketUDPRead { get; set; }

		public EndPoint EndPointUDPRead { get; set; }
		public EndPoint EndPointUDPWrite { get; set; }

		public bool ExecucuteCommand { get; set; }

		public string Command { get; set; }
	}
}
