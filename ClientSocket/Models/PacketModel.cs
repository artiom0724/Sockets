namespace ClientSocket.Models
{
    public class PacketModel
    {
        public PacketModel()
        {
            Number = 0;
            Size = 0;
            FilePosition = 0;
            IsSend = false;
            IsCame = false;
        }
        public long Number { get; set; }

        public long Size { get; set; }

        public long FilePosition { get; set; }

        public bool IsSend { get; set; }

        public bool IsCame { get; set; }
    }
}