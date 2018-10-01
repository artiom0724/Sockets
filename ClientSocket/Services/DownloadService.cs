using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using ClientSocket.Helpers;
using ClientSocket.Models;

namespace ClientSocket.Services
{
    public class DownloadService
    {
        private Socket socket;

        private Socket socketUDP;

        private EndPoint endPoint;

        public ActionResult DownloadFile(string fileName, string downloadingType, string[] parameters, Socket socket, Socket socketUDP, EndPoint endPoint)
        {
            this.socket = socket;
            this.socketUDP = socketUDP;
            this.endPoint = endPoint;

            switch (downloadingType)
            {
                case "tcp":
                    return DownloadFileTCP(fileName, parameters);
                case "udp":
                    return DownloadFileUDP(fileName, parameters);
                default:
                    return new ActionResult();
            }
        }

        public ActionResult DownloadFileTCP(string fileName, string[] parameters)
        {
            long timeAwait = 0;
            var file = File.OpenWrite(fileName);
            var data = new byte[1024];
            var fileModel = new FileModel()
            {
                FileName = file.Name,
                Size = int.Parse(parameters[0])
            };
            if (file.Length > 0 && file.Length < fileModel.Size)
            {
                timeAwait = ContinueDownloading(file);
                if (timeAwait == 0)
                {
                    socket.Send(Encoding.ASCII.GetBytes($"break|"));
                    return new ActionResult();
                }
                fileModel.Packets.Add(new PacketModel()
                {
                    Size = file.Length,
                    IsCame = true,
                    Number = 0
                });
            }
            socket.Send(Encoding.ASCII.GetBytes($"{file.Length}|"));

            while (fileModel.Packets.Where(x => x.IsCame).Sum(x => x.Size) < fileModel.Size)
            {
                DownloadingProcess(file, fileModel);
            }
            return new ActionResult()
            {
                FileSize = fileModel.Size,
                TimeAwait = timeAwait
            };
        }

        private long ContinueDownloading(FileStream file)
        {
            long timeAwait;
            Console.WriteLine("File exist in current download directory.\n" +
              "If it's not one file, then it'll be crashed. Continue downloading?[y\\n]\n");
            var time = DateTime.Now;
            var isContinue = Console.ReadLine().Contains("y");
            timeAwait = (DateTime.Now - time).Milliseconds;
            if (isContinue)
            {
                file.Seek(file.Length, SeekOrigin.Begin);
            }
            else
            {
                return 0;
            }

            return timeAwait;
        }

        private void DownloadingProcess(FileStream file, FileModel fileModel)
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
            Console.WriteLine("Dwonloading... " + (fileModel.Packets.Where(x => x.IsCame).Sum(x => x.Size) * 100 / fileModel.Size) + "%");
        }

        public ActionResult DownloadFileUDP(string fileName, string[] parameters)
        {
            try
            {
                var file = File.OpenWrite(fileName);
                var fileModel = new FileModel()
                {
                    FileName = file.Name,
                    Size = int.Parse(parameters.First())
                };
                if (file.Length > 0)
                {
                    File.Delete(file.Name);
                    file = File.OpenWrite(fileName);
                }
                do
                {
                    FirstDataGetting(file, fileModel);
                } while (socketUDP.Available != 0);
                while (fileModel.Size < fileModel.Packets.Sum(x => x.Size))
                {
                    RegettingMissingPackets(file, fileModel);
                }
                socket.SendTo(Encoding.ASCII.GetBytes("Correct"), endPoint);
                return new ActionResult()
                {
                    FileSize = file.Length,
                    TimeAwait = 0
                };
            }
            catch (FileNotFoundException exc)
            {
                Console.WriteLine(exc.Message);
                return new ActionResult();
            }
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
            } while (socketUDP.Available != 0);
        }

        private void GettingMissingPackets(FileStream file, FileModel fileModel)
        {
            var data = new byte[1024];
            socketUDP.ReceiveFrom(data, ref endPoint);
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
            socketUDP.SendTo(data, endPoint);
        }

        private void FirstDataGetting(FileStream file, FileModel fileModel)
        {
            var data = new byte[1024];
            socketUDP.ReceiveFrom(data, ref endPoint);
            var incomingString = Encoding.ASCII.GetString(data);
            var packetParameters = incomingString.Split('|');
            if (packetParameters.First().Contains("Error"))
            {
                throw new FileNotFoundException();
            }
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