using System.Collections.Generic;
using ClientSocket.Models;

namespace ClientSocket.Services
{
    public class FileModel
    {
        public FileModel()
        {
            Packets = new List<PacketModel>();
        }

        public long Size { get; set; }

        public string FileName { get; set; }

        public List<PacketModel> Packets { get; set; }
    }
}