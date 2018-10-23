using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using ClientSocket.Helpers;
using ClientSocket.Models;

namespace ClientSocket.Services
{
    public class UploadService
    {
        private Socket socket;

        private Socket socketUDP;

        private EndPoint endPoint;

        public ActionResult UploadFile(string fileName, string[] parameters, Socket socket, Socket socketUDP, EndPoint endPoint, ProtocolType type)
        {
            this.socket = socket;
            this.socketUDP = socketUDP;
            this.endPoint = endPoint;
            Console.Clear();

            switch (type)
            {
                case ProtocolType.Tcp:
                    return UploadFileTCP(fileName, parameters);
                case ProtocolType.Udp:
                    return UploadFileUDP(fileName);
                default: 
                    return new ActionResult();
            }
        }

        private ActionResult UploadFileTCP(string fileName, string[] parameters)
        {
            long timeAwait = 0;
            var packetNumber = 0;
            var file = File.OpenRead(fileName);
            var uploaded = int.Parse(parameters[0]);
            var fileModel = new FileModel()
            {
                FileName = fileName,
                Size = file.Length
            };
            if(uploaded != 0)
            {
                timeAwait = ContinueUploading();
                if (timeAwait != 0)
                {
                    fileModel.Packets.Add(new PacketModel()
                    {
                        Number = packetNumber,
                        Size = uploaded,
                        IsSend = true
                    });
                    packetNumber++;
                    file.Seek(uploaded, SeekOrigin.Begin);
                    socket.Send(Encoding.ASCII.GetBytes("continue|"));
                }
                else
                {
                    socket.Send(Encoding.ASCII.GetBytes("restore|"));
                }
            }

            while(fileModel.Packets.Where(x => x.IsSend).Sum(x => x.Size) < fileModel.Size)
            {
                packetNumber = UploadingProcess(packetNumber, file, fileModel);
            }
            var fileLength = file.Length;
            file.Close();
            return new ActionResult()
            {
                FileSize = fileLength,
                TimeAwait = timeAwait
            };
        }

        private int UploadingProcess(int packetNumber, FileStream file, FileModel fileModel)
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
            var percent = (fileModel.Packets.Where(x => x.IsSend).Sum(x => x.Size) * 100 / fileModel.Size);
            percent = percent > 100 ? 100 : percent;
            Console.Write("\rUploading... " + percent + "%");
            return packetNumber;
        }

        private long ContinueUploading()
        {
            long timeAwait;
            Console.WriteLine("File exist in current upload directory.\n" +
              "If it's not one file, after it'll be crashed. Continue uploading?[y\\n]\n");
            var time = DateTime.Now;
            var isContinue = Console.ReadLine().Contains("y");
            timeAwait = (DateTime.Now - time).Milliseconds;
            return isContinue? timeAwait : 0;
        }

        private ActionResult UploadFileUDP(string fileName)
        {
            FileStream file = null;;
           try
            {
                file = File.OpenRead(fileName);

                var fileModel = new FileModel()
                {
                    FileName = file.Name,
                    Size = file.Length
                };
                long packetNumber = 0;

                long partNumber = 0;
                while (fileModel.Packets.Where(x => x.IsCame).Sum(x => x.Size) < file.Length)
                {
                    while (fileModel.Packets.Where(x => x.IsSend).Sum(x => x.Size) < file.Length && partNumber < 64)
                    {
                        packetNumber = FirstSending(file, fileModel, packetNumber);
                        partNumber++;
                    }
                    while (fileModel.Packets.Any(x=> x.IsCame == false))
                    {
                        ResendingMissingPackets(file, fileModel);
                    }
                    partNumber = 0;
                }
                var fileLength = file.Length;
                file.Close();

                return new ActionResult()
                {
                    FileSize = fileLength,
                    TimeAwait = 0
                };
            }
            catch (FileNotFoundException ex)
            {
                Console.WriteLine(ex.Message);
                return new ActionResult();
            }
            catch (SocketException ex)
            {
                file.Close();
                throw ex;
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
                    fileModel.Packets.Clear();
                    return;
                }
                var infoPackets = incomingString.Split("|").Select(x => long.Parse(x));
                foreach (var packet in fileModel.Packets.Where(x=>infoPackets.Contains(x.Number)))
                {
                    packet.IsCame = true;
                }
            } while (socket.Available != 0);
            foreach (var packet in fileModel.Packets)
            {
                var data = new byte[4096];
                var info = Encoding.ASCII.GetBytes($"{packet.Number}|{packet.FilePosition}|");
                data.InsertInStartArray(info);
                file.Seek(packet.FilePosition, SeekOrigin.Begin);
                file.Read(data, info.Length, data.Length - info.Length);
                socketUDP.SendTo(data, endPoint);
                fileModel.Packets.Add(packet);
                var percent = (fileModel.Packets.Sum(x => x.Size) * 100 / fileModel.Size);
                percent = percent > 100 ? 100 : percent;
                Console.Write("\rReupload UDP... " + percent + "%");
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
            var percent = (fileModel.Packets.Where(x => x.IsSend).Sum(x => x.Size) * 100 / fileModel.Size);
            percent = percent > 100 ? 100 : percent;
            Console.Write("\rUpload UDP... " + percent + "%");
            return packetNumber;
        }

    }
}