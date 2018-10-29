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

        private EndPoint ipClient;

        private FileModel fileModel;


        public ActionResult DownloadFile(string fileName, string[] parameters, Socket socket, Socket socketUDP, EndPoint endPoint, ProtocolType type)
        {
            this.socket = socket;
            this.socketUDP = socketUDP;
            this.endPoint = endPoint;
            var returning = new ActionResult();
            switch (type)
            {
                case ProtocolType.Tcp:
                    returning = DownloadFileTCP(fileName, parameters);
                    ipClient = endPoint;
                    return returning;
                case ProtocolType.Udp:
                    returning = DownloadFileUDP(fileName, parameters);
                    ipClient = endPoint;
                    return returning;
                default:
                    return returning;
            }
        }

        public ActionResult DownloadFileTCP(string fileName, string[] parameters)
        {
            long timeAwait = 0;
            var file = File.OpenWrite(fileName);
            var data = new byte[4096];
            if (ipClient != endPoint || file.Name != fileModel.FileName)
            {
                fileModel = new FileModel()
                {
                    FileName = file.Name,
                    Size = int.Parse(parameters[0])
                };
            }
            if (file.Length > 0 && file.Length < fileModel.Size)
            {
                timeAwait = ContinueDownloading(file);
                if (timeAwait == 0)
                {
                    socket.Send(Encoding.ASCII.GetBytes($"break|"));
                    file.Close();
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
                DownloadingProcess(file);
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

        private void DownloadingProcess(FileStream file)
        {
            var data = new byte[4096];
            socket.Receive(data);
            long packetNumber, filePosition;
            byte[] writedData ;
            using (var stream = new PacketReader(data))
            {
                 packetNumber = stream.ReadInt64();
                 filePosition = stream.ReadInt64();
                 writedData = stream.ReadBytes(data.Length - 2*sizeof(long));
            }
            if (fileModel.Size - file.Length < writedData.Length)
            {
                writedData = writedData.SubArray(0, fileModel.Size - file.Length);
            }
            file.Write(writedData, 0, writedData.Length);
            fileModel.Packets.Add(new PacketModel()
            {
                Size = writedData.LongLength,
                IsCame = true,
                Number = packetNumber
            });
            Console.Write("\rDownloading... " + (fileModel.Packets.Where(x => x.IsCame).Sum(x => x.Size) * 100 / fileModel.Size) + "%");
        }

        public ActionResult DownloadFileUDP(string fileName, string[] parameters)
        {
            var file = File.OpenWrite(fileName);
            try
            {
                fileModel = new FileModel()
                {
                    FileName = file.Name,
                    Size = int.Parse(parameters.First())
                };
                if (file.Length > 0)
                {
                    var filename = file.Name;
                    file.Close();
                    File.Delete(file.Name);
                    file = File.OpenWrite(fileName);
                }
                long countCamingPackets = 0;
                while (fileModel.Packets.Sum(x => x.Size) < fileModel.Size)
                {
                    do
                    {
                        FirstDataGetting(file);
                        countCamingPackets++;
                    } while (socketUDP.Available != 0);
                    while (countCamingPackets != 64 && file.Length < fileModel.Size)
                    {
                        RegettingMissingPackets(file, ref countCamingPackets);
                        countCamingPackets = 0;
                    }
                    socket.Send(Encoding.ASCII.GetBytes("Correct|"));
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

        private void RegettingMissingPackets(FileStream file, ref long countCamingPackets)
        {
            var camingPackets = fileModel.Packets.Select(x => x.Number)).ToList();
            while (true)
            {
                if (!camingPackets.Any())
                {
                    break;
                }
                SendCamingPackagesNumbers(camingPackets);
            }
            do
            {
                GettingMissingPackets(file);
                countCamingPackets++;
            } while (socket.Available != 0);
        }

        private void GettingMissingPackets(FileStream file)
        {
            var data = new byte[4096];
            var endpoint = (EndPoint)(new IPEndPoint(((IPEndPoint)(socket.RemoteEndPoint)).Address, (((IPEndPoint)(socket.RemoteEndPoint)).Port + 2)));
            socketUDP.ReceiveFrom(data, ref endpoint);
            long packetNumber, filePosition;
            byte[] writedData;
            using (var stream = new PacketReader(data))
            {
                packetNumber = stream.ReadInt64();
                filePosition = stream.ReadInt64();
                writedData = stream.ReadBytes(data.Length - 2 * sizeof(long));
            }
            if (fileModel.Packets.Any(x => x.Number == packetNumber))
            {
                return;
            }
            if (fileModel.Size - file.Length < writedData.Length)
            {
                writedData = writedData.SubArray(0, fileModel.Size - file.Length);
            }
            file.Seek(filePosition, SeekOrigin.Begin);
            file.Write(writedData, 0, writedData.Length);
            fileModel.Packets.Add(new PacketModel()
            {
                Size = writedData.LongLength,
                IsCame = true,
                Number = packetNumber,
                FilePosition = filePosition
            });
            Console.Write("\rRegetting... " + (fileModel.Packets.Where(x => x.IsCame).Sum(x => x.Size) * 100 / fileModel.Size) + "%");
        }

        private void SendCamingPackagesNumbers(List<long> camingPackets)
        {
            int offset = 0;
            using (var stream = new PacketWriter())
            {
                while (camingPackets.Any())
                {
                    var number = camingPackets.FirstOrDefault();
                    if (offset + sizeof(long) > 4096)
                    {
                        break;
                    }
                    stream.Write(number);
                    offset += sizeof(long);
                    camingPackets.Remove(number);
                }
                socket.Send(stream.ToByteArray());
            }
        }

        private void FirstDataGetting(FileStream file)
        {
            var data = new byte[4096];
            var endpoint = (EndPoint)(new IPEndPoint(((IPEndPoint)(socket.RemoteEndPoint)).Address, (((IPEndPoint)(socket.RemoteEndPoint)).Port + 2)));
            socketUDP.ReceiveFrom(data, ref endpoint);
            var incomingString = Encoding.ASCII.GetString(data);
            if (incomingString.Contains("Error"))
            {
                throw new FileNotFoundException();
            }
            
            long packetNumber, filePosition;
            byte[] writedData;
            using (var stream = new PacketReader(data))
            {
                packetNumber = stream.ReadInt64();
                filePosition = stream.ReadInt64();
                writedData = stream.ReadBytes(data.Length - 2 * sizeof(long));
            }
            if (fileModel.Packets.Any(x => x.Number == packetNumber))
            {
                return;
            }
            if (fileModel.Size - file.Length < writedData.Length)
            {
                writedData = writedData.SubArray(0, fileModel.Size - file.Length);
            }
            file.Seek(filePosition, SeekOrigin.Begin);
            file.Write(writedData, 0, writedData.Length);
            fileModel.Packets.Add(new PacketModel()
            {
                Size = writedData.LongLength,
                IsCame = true,
                Number = packetNumber,
                FilePosition = filePosition
            });
            Console.Write("\rGetting... " + (fileModel.Packets.Where(x => x.IsCame).Sum(x => x.Size) * 100 / fileModel.Size) + "%");
        }
    }
}