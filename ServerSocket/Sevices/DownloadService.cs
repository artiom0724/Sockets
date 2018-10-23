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

        public void DownloadFile(Socket socket, EndPoint endPoint, Socket socketUDP, ServerCommand command, ProtocolType type)
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

        private string CheckFileExists(FileStream file)
        {
            var parameters = Encoding.ASCII.GetBytes($"{file.Length}|");
            socket.Send(parameters);
            var data = new byte[4096];
            socket.Receive(data);
            var incomingString = Encoding.ASCII.GetString(data);
            return incomingString;
        }

        private long SendingProcess(FileStream file, FileModel fileModel, long packetNumber)
        {
            var data = new byte[4096];
            var info = Encoding.ASCII.GetBytes($"{packetNumber}|{file.Position}|");
            var packet = new PacketModel()
            {
                Number = packetNumber,
                IsSend = true,
                Size = data.Length - info.Length
            };
            packetNumber++;
            data.InsertInStartArray(info);
            file.Read(data, info.Length, data.Length - info.Length);
            socket.Send(data);
            fileModel.Packets.Add(packet);
            Console.WriteLine("\rSending... " + (fileModel.Packets.Where(x => x.IsSend).Sum(x => x.Size) * 100 / fileModel.Size) + "%");
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
                socket.Send(Encoding.ASCII.GetBytes($"{file.Length}|"));
                while (file.Length > fileModel.Packets.Where(x => x.IsCame).Sum(x => x.Size))
                {
                    while (fileModel.Packets.Where(x => x.IsSend).Sum(x => x.Size) < file.Length && partCamingPackets < 64)
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
                var response = Encoding.ASCII.GetBytes($"Error|");
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
                var infoPackets = incomingString.Split("|").Select(x => long.Parse(x));
                foreach(var packet in fileModel.Packets.Where(x => infoPackets.Contains(x.Number)))
                {
                    packet.IsCame = true;
                }
            } while (socket.Available != 0);
            foreach (var packet in fileModel.Packets.Where(x=>x.IsCame == false))
            {
                var data = new byte[4096];
                var info = Encoding.ASCII.GetBytes($"{packet.Number}|{packet.FilePosition}|");
                data.InsertInStartArray(info);
                file.Seek(packet.FilePosition, SeekOrigin.Begin);
                file.Read(data, info.Length, data.Length - info.Length);
                socketUDP.SendTo(data, endPoint);
                fileModel.Packets.Add(packet);
                Console.WriteLine("\rResending UDP... " + (fileModel.Packets.Sum(x => x.Size) * 100 / fileModel.Size));
            }
        }

        private long FirstSending(FileStream file, FileModel fileModel, long packetNumber)
        {
            var data = new byte[4096];
            var info = Encoding.ASCII.GetBytes($"{packetNumber}|{file.Position}|");
            var packet = new PacketModel()
            {
                Number = packetNumber,
                IsSend = true,
                Size = data.Length - info.Length,
                FilePosition = file.Position
            };
            packetNumber++;
            data.InsertInStartArray(info);
            file.Read(data, info.Length, data.Length - info.Length);
            socketUDP.SendTo(data, endPoint);
            fileModel.Packets.Add(packet);
            Console.WriteLine("\rSending UDP... " + (fileModel.Packets.Where(x => x.IsSend).Sum(x => x.Size) * 100 / fileModel.Size) + "%");
            return packetNumber;
        }
    }
}
