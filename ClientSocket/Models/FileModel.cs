using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using ClientSocket.Models;

namespace ClientSocket.Services
{
    public class FileModel
    {
		public FileStream fileStream;

		public FileModel()
        {
            Packets = new List<PacketModel>();
        }

		public Socket socket { get; set; }

        public long Size { get; set; }

        public string FileName { get; set; }

		public long PacketNumber { get; set; }

		public long PacketCount { get; set; }

        public List<PacketModel> Packets { get; set; }
    }
}