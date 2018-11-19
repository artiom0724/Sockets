using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ClientSocket.Helpers
{
    public class PacketReader : BinaryReader
    {
        public PacketReader(byte[] input) : base(new MemoryStream(input))
        {
        }
    }

    public class PacketWriter : BinaryWriter
    {
        private MemoryStream memoryStream;

        public PacketWriter()
        {
            memoryStream = new MemoryStream();
            OutStream = memoryStream;
        }

        public byte[] ToByteArray()
        {
            return memoryStream.ToArray();
        }

		public void Clear()
		{
			memoryStream.SetLength(0);
		}
    }
}
