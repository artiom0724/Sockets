using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using ClientSocket.Helpers;
using ClientSocket.Models;
using ClientSocket.Services;
using ServerSocket.Models;

namespace ServerSocket.Sevices
{
    public class DownloadService
    {
        private Socket socket;

        private Socket socketUDP;

        private EndPoint endPoint;
        private EndPoint endPointRead;

		private Socket socketUDPRead;

        public void DownloadFile(Socket socket, EndPoint endPoint, Socket socketUDP, ServerCommand command, ProtocolType type, bool completeExecute = false)
        {
            this.socket = socket;
            this.socketUDP = socketUDP;
            this.endPoint = endPoint;

            switch (type)
            {
                case ProtocolType.Tcp:
                    DownloadFileTCP(command);
                    return;
                case ProtocolType.Udp:
                    DownloadFileUDP(command);
                    return;
            }
        }

        private void DownloadFileTCP(ServerCommand command)
        {
            try
            {
                var file = File.OpenRead(command.Parameters.First());
                string incomingString = CheckFileExists(file);
                if (incomingString.Contains("break"))
                {
                    return;
                }
                var sendedSize = long.Parse(incomingString.Split('|').First());
                var fileModel = new FileModel()
                {
                    FileName = file.Name,
                    Size = file.Length
                };
                long packetNumber = 0;
                if (sendedSize > 0)
                {
                    file.Seek(sendedSize, SeekOrigin.Begin);
                    fileModel.Packets.Add(new PacketModel()
                    {
                        Size = sendedSize,
                        Number = packetNumber,
                        IsSend = true
                    });
                    packetNumber++;
                }

				while (fileModel.Packets.Where(x => x.IsSend).Sum(x => x.Size) < file.Length)
				{
					packetNumber = SendingProcess(file, fileModel, packetNumber);
				}
                file.Close();
            }
            catch (FileNotFoundException ex)
            {
                var response = Encoding.ASCII.GetBytes($"Error|");
                socket.Send(response);
                Console.WriteLine(ex);
            }
        }

		public bool ContinueExecute(Socket _handler, EndPoint _endPointUDP, EndPoint _endPointUDPRead, Socket _socketUDP, Socket _socketUDPRead, ServerCommand _command, ProtocolType type)
		{
			this.socket = _handler;
			this.socketUDP = _socketUDP;
			this.endPoint = _endPointUDP;
			this.endPointRead = _endPointUDPRead;
			this.socketUDPRead = _socketUDPRead;

			switch (type)
			{
				case ProtocolType.Tcp:
					DownloadFileTCP(_command);
					return true;
				case ProtocolType.Udp:
					return DownloadFileUDPContinue(_command);
			}
			return false;
		}

		List<FileModel> fileModels = new List<FileModel>();

		private bool DownloadFileUDPContinue(ServerCommand command)
		{
			var fileModel = fileModels.FirstOrDefault(x => x.socket == socket);
			var file = fileModel == null? File.OpenRead(command.Parameters.First()) : fileModel.fileStream;
			if (fileModel == null)
			{
				fileModel = new FileModel()
				{
					FileName = file.Name,
					socket = socket,
					Size = file.Length,
					PacketNumber = 0,
					PacketCount = 0,
					fileStream = file
				};
				fileModels.Add(fileModel);
				socket.Send(new byte[4096].InsertInStartArray(Encoding.ASCII.GetBytes($"{file.Length}|")));
				return false;
			}
			if (file.Length <= fileModel.Packets.Sum(x => x.Size))
			{
				fileModels.Remove(fileModel);
				file.Close();
				return true;
			}
			if (fileModel.PacketCount == 16)
			{
				fileModel.PacketCount = 0;
				var infoData = new byte[4096];
				socketUDPRead.ReceiveFrom(infoData, ref endPointRead);
				return file.Length == fileModel.Packets.Sum(x => x.Size);
			}
			fileModel.PacketNumber = FirstSending(file, fileModel, fileModel.PacketNumber);
			fileModel.PacketCount++;
			var filelength = file.Length;
			var resultSize = fileModel.Packets.Sum(x => x.Size);
			if (file.Length <= resultSize)
			{
				fileModels.Remove(fileModel);
				file.Close();
			}
			return filelength <= resultSize;
		}

		private string CheckFileExists(FileStream file)
        {
            var parameters = Encoding.ASCII.GetBytes($"{file.Length}|");
			var sendingBytes = (new byte[4096]).InsertInStartArray(parameters);
            socket.Send(sendingBytes);
            var data = new byte[4096];
            socket.Receive(data);
            var incomingString = Encoding.ASCII.GetString(data);
            return incomingString;
        }

        private long SendingProcess(FileStream file, FileModel fileModel, long packetNumber)
        {
            var data = new byte[4096];
            using (var stream = new PacketWriter())
            {
                stream.Write(packetNumber);
				long fileposition = file.Position;

				stream.Write(fileposition);
                data.InsertInStartArray(stream.ToByteArray());
            }
            var packet = new PacketModel()
            {
                Number = packetNumber,
                IsSend = true,
                Size = data.Length - 2*sizeof(long),
				FilePosition = file.Position
            };
            packetNumber++;
            file.Read(data, 2 * sizeof(long), data.Length - 2 * sizeof(long));
            socket.Send(data);
            fileModel.Packets.Add(packet);
            Console.Write("\rSending... " + (fileModel.Packets.Where(x => x.IsSend).Sum(x => x.Size) * 100 / fileModel.Size) + "%");
            return packetNumber;
        }

        private void DownloadFileUDP(ServerCommand command)
        {
            try
            {
                var file = File.OpenRead(command.Parameters.First());

                var fileModel = new FileModel()
                {
                    FileName = file.Name,
                    Size = file.Length
                };
                long packetNumber = 0, partCamingPackets = 0;
                socket.Send(new byte[4096].InsertInStartArray(Encoding.ASCII.GetBytes($"{file.Length}|")));
                while (file.Length > fileModel.Packets.Where(x => x.IsCame).Sum(x => x.Size))
                {
                    while (fileModel.Packets.Where(x => x.IsSend).Sum(x => x.Size) < file.Length && partCamingPackets < 16)
                    {
                        packetNumber = FirstSending(file, fileModel, packetNumber);
                        partCamingPackets++;
                    }
                    partCamingPackets = 0;
                    while (fileModel.Packets.Any(x=>x.IsCame == false))
                    {
                        ResendingMissingPackets(file, fileModel);
                    }
                }
                file.Close();
            }
            catch (FileNotFoundException ex)
            {
                var response = new byte[4096].InsertInStartArray(Encoding.ASCII.GetBytes($"Error|"));
                socket.Send(response);
                Console.WriteLine(ex);
            }
        }

        private void ResendingMissingPackets(FileStream file, FileModel fileModel)
        {
            do
            {
                var infoCaming = new byte[4096];

                socket.Receive(infoCaming);

                var incomingString = Encoding.ASCII.GetString(infoCaming);
                if (incomingString.Contains("Correct"))
                {
                    fileModel.Packets.ForEach(x => x.IsCame = true);
                    return;
                }
                List<long> infoPackets = GetInfoPackets(infoCaming);
                foreach (var packet in fileModel.Packets.Where(x => infoPackets.Contains(x.Number)))
                {
                    packet.IsCame = true;
                }
            } while (socket.Available != 0);
            foreach (var packet in fileModel.Packets.Where(x=>x.IsCame == false))
            {
                ResendingData(file, fileModel, packet);
            }
        }

        private void ResendingData(FileStream file, FileModel fileModel, PacketModel packet)
        {
            var data = new byte[4096];
            using (var stream = new PacketWriter())
            {
                stream.Write(packet.Number);
                stream.Write(packet.FilePosition);
                data.InsertInStartArray(stream.ToByteArray());
            }
            DataSending(file, fileModel, packet, data);
            Console.Write("\rResending UDP... " + (fileModel.Packets.Sum(x => x.Size) * 100 / fileModel.Size));
        }

        private void DataSending(FileStream file, FileModel fileModel, PacketModel packet, byte[] data)
        {
            file.Seek(packet.FilePosition, SeekOrigin.Begin);
            file.Read(data, 2 * sizeof(long), data.Length - 2 * sizeof(long));
            socketUDP.SendTo(data, endPoint);
            fileModel.Packets.Add(packet);
        }

        private static List<long> GetInfoPackets(byte[] infoCaming)
        {
            var infoPackets = new List<long>();
            using (var stream = new PacketReader(infoCaming))
            {
                while (true)
                {
                    var packetNum = stream.ReadInt64();
                    if (packetNum == 0)
                    {
                        break;
                    }
                    infoPackets.Add(packetNum);
                }
            }

            return infoPackets;
        }

        private long FirstSending(FileStream file, FileModel fileModel, long packetNumber)
        {
            var data = new byte[4096];
			file.Seek(fileModel.Packets.Sum(x => x.Size), SeekOrigin.Begin);

			using (var stream = new PacketWriter())
            {
                stream.Write(packetNumber);
				long fileposition = file.Position;

				stream.Write(fileposition);
				data.InsertInStartArray(stream.ToByteArray());
            }
            var packet = new PacketModel()
            {
                Number = packetNumber,
                IsSend = true,
                Size = data.Length - 2*sizeof(long),
                FilePosition = file.Position
            };
            packetNumber++;
            file.Read(data, 2 * sizeof(long), data.Length - 2 * sizeof(long));
            socketUDP.SendTo(data, endPoint);
            fileModel.Packets.Add(packet);
            Console.Write("\rSending UDP... " + (fileModel.Packets.Sum(x => x.Size) * 100 / fileModel.Size) + "%");
            return packetNumber;
        }
    }
}
