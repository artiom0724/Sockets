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
            if (fileModel == null || ipClient != endPoint || file.Name != fileModel.FileName)
            {
                fileModel = new FileModel()
                {
                    FileName = file.Name,
                    Size = long.Parse(parameters[0])
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
			var windowPackets = 0;

			while (fileModel.Packets.Where(x => x.IsCame).Sum(x => x.Size) < fileModel.Size)
            {
				if (DownloadingProcess(file))
				{
					windowPackets++;
				}
				if(windowPackets == 16 || fileModel.Packets.Where(x => x.IsCame).Sum(x => x.Size) < fileModel.Size)
				{
					windowPackets = 0;
					socket.Send(Encoding.ASCII.GetBytes("next|"));
				}
            }
            var fileModelSize = fileModel.Size;
            fileModel = null;
            file.Close();
            return new ActionResult()
            {
                FileSize = fileModelSize,
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

		private bool DownloadingProcess(FileStream file)
		{
			var data = new byte[4096];

			var received = socket.Receive(data);
			if (received <= 0)
			{
				return false;
			}
			long packetNumber, filePosition;
			byte[] writedData;
			using (var stream = new PacketReader(data))
			{
				packetNumber = stream.ReadInt64();
				filePosition = stream.ReadInt64();
				writedData = stream.ReadBytes(data.Length - 2 * sizeof(long));
			}
			if(packetNumber != (fileModel.Packets.Count))
			{
				socket.Send(Encoding.ASCII.GetBytes($"error|"));
				return false;
			}
			if (fileModel.Size - file.Length < writedData.Length)
			{
				writedData = writedData.SubArray(0, fileModel.Size - file.Length);
			}
			Console.WriteLine($"  {packetNumber} ===> {filePosition}");
			if (filePosition % 4080 == 0)
			{
				file.Seek(filePosition, SeekOrigin.Begin);
			}
			file.Write(writedData, 0, writedData.Length);
			fileModel.Packets.Add(new PacketModel()
			{
				Size = writedData.LongLength,
				IsCame = true,
				Number = packetNumber
			});
			Console.Write("\rDownloading... " + (fileModel.Packets.Where(x => x.IsCame).Sum(x => x.Size) * 100 / fileModel.Size) + "%");
			return true;
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
                var countCamingPackets = 0;
                var countErrors = 0;
                while (fileModel.Packets.Sum(x => x.Size) < fileModel.Size)
                {
                    do
                    {
                        var result = FirstDataGetting(file);
                        if (result)
                        {
                            countCamingPackets++;
                        }
                        else
                        {
                            countErrors++;
                        }
                    } while (countErrors < 5 && countCamingPackets < 16 && fileModel.Packets.Sum(x => x.Size) < fileModel.Size);
                    countErrors = 0;
                    while (countCamingPackets != 16 && file.Length < fileModel.Size )
                    {
                        RegettingMissingPackets(file, ref countCamingPackets);
                    }
                    countCamingPackets = 0;
                    socket.Send(Encoding.ASCII.GetBytes("Correct|"));
                }
                var fileLength = file.Length;
                file.Close();
                return new ActionResult()
                {
                    FileSize = fileLength,
                    TimeAwait = 0
                };
            }
            catch (FileNotFoundException exc)
            {
                file.Close();
                Console.WriteLine(exc);
                return new ActionResult();
            }
        }

        

        private void RegettingMissingPackets(FileStream file, ref int countCamingPackets)
        {
            var camingPackets = fileModel.Packets.TakeLast(16).Where(x => x.IsCame).Select(x => x.Number).ToList();
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
                if (GettingMissingPackets(file))
                {
                    countCamingPackets++;
                }
            } while (countCamingPackets < 16);
        }

        private bool GettingMissingPackets(FileStream file)
        {
            var returning = DataGetting(file);
            Console.Write("\rRegetting... " + (fileModel.Packets.Where(x => x.IsCame).Sum(x => x.Size) * 100 / fileModel.Size) + "%");
            return returning;
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

        private bool DataGetting(FileStream file)
        {
            var data = new byte[4096];
            var received = socketUDP.ReceiveFrom(data, ref endPoint);
            if (received <= 0)
            {
                return false;
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
                return true;
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
            return true;
        }

        private bool FirstDataGetting(FileStream file)
        {
            var returning = DataGetting(file);
            Console.Write("\rGetting... " + (fileModel.Packets.Where(x => x.IsCame).Sum(x => x.Size) * 100 / fileModel.Size) + "%");
            return returning;
        }
    }
}