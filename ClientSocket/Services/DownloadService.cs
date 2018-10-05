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

        private EndPoint endPoint;

        public ActionResult DownloadFile(string fileName, string[] parameters, Socket socket, EndPoint endPoint)
        {
            this.socket = socket;
            this.endPoint = endPoint;

            switch (socket.ProtocolType)
            {
                case ProtocolType.Tcp:
                    return DownloadFileTCP(fileName, parameters);
                case ProtocolType.Udp:
                    return DownloadFileUDP(fileName, parameters);
                default:
                    return new ActionResult();
            }
        }

        public ActionResult DownloadFileTCP(string fileName, string[] parameters)
        {
            long timeAwait = 0;
            var file = File.OpenWrite(fileName);
            var data = new byte[4096];
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
            file.Close();
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
            var data = new byte[4096];
            if (socket.Poll(20000, SelectMode.SelectError))
            {
                throw new SocketException((int)SocketError.ConnectionReset);
            }
            socket.Receive(data);
            var incomingString = Encoding.ASCII.GetString(data);
            var packetParameters = incomingString.Split('|');
            var parametersSize = Encoding.ASCII.GetBytes($"{packetParameters[0]}|{packetParameters[1]}|").Count();
            var writedData = data.SubArray(parametersSize, data.Length - parametersSize);
            if (fileModel.Size - file.Length < writedData.Length)
            {
                writedData = writedData.SubArray(0, fileModel.Size - file.Length);
            }
            file.Write(writedData, 0, writedData.Length);
            fileModel.Packets.Add(new PacketModel()
            {
                Size = writedData.LongLength,
                IsCame = true,
                Number = long.Parse(packetParameters[0])
            });
            Console.Write("\rDownloading... " + (fileModel.Packets.Where(x => x.IsCame).Sum(x => x.Size) * 100 / fileModel.Size) + "%");
        }

        public ActionResult DownloadFileUDP(string fileName, string[] parameters)
        {
            var file = File.OpenWrite(fileName);
            try
            {
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
                long countCamingPackets = 0;
                while (file.Length < fileModel.Size)
                {
                    do
                    {
                        FirstDataGetting(file, fileModel);
                        countCamingPackets++;
                    } while (socket.Available != 0);
                    while (countCamingPackets == 64 && file.Length < fileModel.Size)
                    {
                        RegettingMissingPackets(file, fileModel, ref countCamingPackets);
                        countCamingPackets = 0;
                    }
                    socket.SendTo(Encoding.ASCII.GetBytes("Correct|"), endPoint);
                }
                file.Close();
                return new ActionResult()
                {
                    FileSize = file.Length,
                    TimeAwait = 0
                };
            }
            catch (FileNotFoundException exc)
            {
                file.Close();
                Console.WriteLine(exc.Message);
                return new ActionResult();
            }
        }

        private void RegettingMissingPackets(FileStream file, FileModel fileModel, ref long countCamingPackets)
        {
            var camingPackets = fileModel.Packets.Select(x => Encoding.ASCII.GetBytes($"{x.Number.ToString()}|")).ToList();
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
                countCamingPackets++;
            } while (socket.Available != 0);
        }

        private void GettingMissingPackets(FileStream file, FileModel fileModel)
        {
            var data = new byte[4096];
            if (socket.Poll(20000, SelectMode.SelectError))
            {
                throw new SocketException((int)SocketError.ConnectionReset);
            }
            socket.ReceiveFrom(data, ref endPoint);
            var incomingString = Encoding.ASCII.GetString(data);
            var packetParameters = incomingString.Split('|');
            if(fileModel.Packets.Any(x=>x.Number == long.Parse(packetParameters[0])))
            {
                return;
            }
            var parametersSize = Encoding.ASCII.GetBytes($"{packetParameters[0]}|{packetParameters[1]}|").Count();
            var writedData = data.SubArray(parametersSize, data.Length - parametersSize);
            if (fileModel.Size - file.Length < writedData.Length)
            {
                writedData = writedData.SubArray(0, fileModel.Size - file.Length);
            }
            file.Seek(long.Parse(packetParameters[1]), SeekOrigin.Begin);
            file.Write(writedData, 0, writedData.Length);
            fileModel.Packets.Add(new PacketModel()
            {
                Size = writedData.LongLength,
                IsCame = true,
                Number = long.Parse(packetParameters[0]),
                FilePosition = long.Parse(packetParameters[1])
            });
            Console.Write("\rRegetting... " + (fileModel.Packets.Where(x => x.IsCame).Sum(x => x.Size) * 100 / fileModel.Size) + "%");
        }

        private void SendCamingPackagesNumbers(List<byte[]> camingPackets)
        {
            int offset = 0;
            var data = new byte[4096];
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
            var data = new byte[4096];
            if (socket.Poll(20000, SelectMode.SelectError))
            {
                throw new SocketException((int)SocketError.ConnectionReset);
            }
            socket.ReceiveFrom(data, ref endPoint);
            var incomingString = Encoding.ASCII.GetString(data);
            var packetParameters = incomingString.Split('|');
            if (packetParameters.First().Contains("Error"))
            {
                throw new FileNotFoundException();
            }
            if (fileModel.Packets.Any(x => x.Number == long.Parse(packetParameters[0])))
            {
                return;
            }
            var parametersSize = Encoding.ASCII.GetBytes($"{packetParameters[0]}|{packetParameters[1]}|").Count();
            var writedData = data.SubArray(parametersSize, data.Length - parametersSize);
            if (fileModel.Size - file.Length < writedData.Length)
            {
                writedData = writedData.SubArray(0, fileModel.Size - file.Length);
            }
            file.Seek(long.Parse(packetParameters[1]), SeekOrigin.Begin);
            file.Write(writedData, 0, writedData.Length);
            fileModel.Packets.Add(new PacketModel()
            {
                Size = writedData.LongLength,
                IsCame = true,
                Number = long.Parse(packetParameters[0]),
                FilePosition = long.Parse(packetParameters[1])
            });
            Console.Write("\rGetting... " + (fileModel.Packets.Where(x => x.IsCame).Sum(x => x.Size) * 100 / fileModel.Size) + "%");
        }
    }
}