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
    public class UploadService
    {
        private Socket socket;

        private EndPoint endPoint;

        public void UploadFile(Socket socket, EndPoint endPoint, ServerCommand command)
        {
            this.socket = socket;
            this.endPoint = endPoint;
            switch (command.Parameters[1])
            {
                case "tcp":
                    UploadFileTCP(command);
                    return;
                case "udp":
                    UploadFileUDP(command);
                    return;
            }
        }

        private void UploadFileTCP(ServerCommand command)
        {
            var file = File.OpenWrite(command.Parameters.First());
            var fileModel = new FileModel()
            {
                FileName = file.Name,
                Size = int.Parse(command.Parameters.First())
            };
            var data = new byte[1024];
            if (file.Length > 0)
            {
                socket.Send(Encoding.ASCII.GetBytes($"{file.Length}|"));
                socket.Receive(data);
                if (Encoding.ASCII.GetString(data).Split("|").First().Contains("continue"))
                {
                    file.Seek(file.Length, SeekOrigin.Begin);
                    fileModel.Packets.Add(new PacketModel()
                    {
                        Number = 0,
                        Size = file.Length,
                        IsCame = true
                    });
                }
                else
                {
                    File.Delete(file.Name);
                    file = File.OpenWrite(command.Parameters.First());
                }
            }

            while (fileModel.Packets.Where(x => x.IsCame).Sum(x => x.Size) < fileModel.Size)
            {
                GettingProcess(file, fileModel);
            }
        }

        private void GettingProcess(FileStream file, FileModel fileModel)
        {
            var data = new byte[1024];
            socket.Receive(data);
            var incomingString = Encoding.ASCII.GetString(data);
            var packetParameters = incomingString.Split('|');
            var parametersSize = Encoding.ASCII.GetBytes($"{packetParameters[0]}|{packetParameters[1]}|").Count();
            var writedData = data.SubArray(parametersSize, data.Length - parametersSize);
            file.Write(writedData, 0, writedData.Length);
            fileModel.Packets.Add(new PacketModel()
            {
                Size = data.Length - parametersSize,
                IsCame = true,
                Number = long.Parse(packetParameters[0])
            });
            Console.Clear();
            Console.WriteLine("Getting... " + (fileModel.Packets.Where(x => x.IsCame).Sum(x => x.Size) * 100 / fileModel.Size) + "%");
        }

        private void UploadFileUDP(ServerCommand command)
        {
            var file = File.OpenWrite(command.Parameters.First());
            var fileModel = new FileModel()
            {
                FileName = file.Name,
                Size = int.Parse(command.Parameters.First())
            };
            if (file.Length > 0)
            {
                File.Delete(file.Name);
                file = File.OpenWrite(command.Parameters.First());
            }

            do
            {
                FirstDataGetting(file, fileModel);
            } while (socket.Available != 0);
            while (fileModel.Size < fileModel.Packets.Sum(x => x.Size))
            {
                RegettingMissingPackets(file, fileModel);
            }
            socket.SendTo(Encoding.ASCII.GetBytes("Correct"), endPoint);
        }

        private void RegettingMissingPackets(FileStream file, FileModel fileModel)
        {
            var camingPackets = fileModel.Packets.Select(x => Encoding.ASCII.GetBytes($"{x.Number.ToString()}")).ToList();
            while (true)
            {
                SendCamingPackagesNumbers(camingPackets);
                if (!camingPackets.Any())
                {
                    break;
                }
            }
            do
            {
                GettingMissingPackets(file, fileModel);
            } while (socket.Available != 0);
        }

        private void GettingMissingPackets(FileStream file, FileModel fileModel)
        {
            var data = new byte[1024];
            socket.ReceiveFrom(data, ref endPoint);
            var incomingString = Encoding.ASCII.GetString(data);
            var packetParameters = incomingString.Split('|');
            var parametersSize = Encoding.ASCII.GetBytes($"{packetParameters[0]}|{packetParameters[1]}|").Count();
            var writedData = data.SubArray(parametersSize, data.Length - parametersSize);
            file.Seek(long.Parse(packetParameters[1]), SeekOrigin.Begin);
            file.Write(writedData, 0, writedData.Length);
            fileModel.Packets.Add(new PacketModel()
            {
                Size = data.Length - parametersSize,
                IsCame = true,
                Number = long.Parse(packetParameters[0]),
                FilePosition = long.Parse(packetParameters[1])
            });
            Console.Clear();
            Console.WriteLine("Regetting... " + (fileModel.Packets.Where(x => x.IsCame).Sum(x => x.Size) * 100 / fileModel.Size) + "%");
        }

        private void SendCamingPackagesNumbers(List<byte[]> camingPackets)
        {
            int offset = 0;
            var data = new byte[1024];
            while (true)
            {
                var number = camingPackets.First();
                if (offset + number.Length > data.Length)
                {
                    break;
                }
                data.InsertInArray(offset, number);
                offset += number.Length;
                camingPackets.Remove(number);
            }
            socket.SendTo(data, endPoint);
        }

        private void FirstDataGetting(FileStream file, FileModel fileModel)
        {
            var data = new byte[1024];
            socket.ReceiveFrom(data, ref endPoint);
            var incomingString = Encoding.ASCII.GetString(data);
            var packetParameters = incomingString.Split('|');
            var parametersSize = Encoding.ASCII.GetBytes($"{packetParameters[0]}|{packetParameters[1]}|").Count();
            var writedData = data.SubArray(parametersSize, data.Length - parametersSize);
            file.Seek(long.Parse(packetParameters[1]), SeekOrigin.Begin);
            file.Write(writedData, 0, writedData.Length);
            fileModel.Packets.Add(new PacketModel()
            {
                Size = data.Length - parametersSize,
                IsCame = true,
                Number = long.Parse(packetParameters[0]),
                FilePosition = long.Parse(packetParameters[1])
            });
            Console.Clear();
            Console.WriteLine("Getting... " + (fileModel.Packets.Where(x => x.IsCame).Sum(x => x.Size) * 100 / fileModel.Size) + "%");
        }
    }
}
