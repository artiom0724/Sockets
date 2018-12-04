using ClientSocket.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace PingSocket.Models
{
	public class IcmpPackage
	{
		public IcmpType Type { get; set; }
		public string Payload { get; set; }

		public byte[] Build()
		{
			var package = new byte[] { (byte)Type, 0, 0, 0, 0, 0, 0, 0 }.Concat(Payload.StringToByteArray()).ToArray();

			var checksum = BitConverter.GetBytes(ComputeChecksum(package));
			package[2] = checksum[1];
			package[3] = checksum[0];

			return package;
		}

		public static IcmpPackage RestorePackage(byte[] package) => new IcmpPackage
		{ Type = (IcmpType)package.Skip(20).First(), Payload = package.Skip(28).ToArray().ByteArrayToString() };

		private ushort ComputeChecksum(byte[] payLoad)
		{
			uint checksum = 0;
			for (var i = 0; i < payLoad.Length / 2; i++)
			{
				checksum = checksum + (ushort)((ushort)(payLoad[i * 2] << 8) | payLoad[i * 2 + 1]);
			}

			checksum += payLoad.Length % 2 != 0 ? payLoad.Last() : (uint)0;
			checksum = (checksum >> 16) + (checksum & 0xFFFF);
			checksum += checksum >> 16;

			return (ushort)~checksum;
		}
	}
}
