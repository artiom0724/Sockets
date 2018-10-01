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

        public ActionResult UploadFile(string fileName, string uploadType, string[] parameters, Socket socket, Socket socketUDP, EndPoint endPoint)
        {
            this.socket = socket;
            this.socketUDP = socketUDP;
            this.endPoint = endPoint;
            switch(uploadType)
            {
                case "tcp":
                    return UploadFileTCP(fileName, parameters);
                case "udp":
                    return UploadFileUDP(fileName);
                default: 
                    return new ActionResult();
            }
        }

        private ActionResult UploadFileTCP(string fileName, string[] parameters)
        {
            long timeAvait = 0;
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
                timeAvait = ContinueUploading(file);
                if (timeAvait != 0)
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

            return new ActionResult()
            {
                FileSize = file.Length,
                TimeAwait = timeAvait
            };
        }

        private int UploadingProcess(int packetNumber, FileStream file, FileModel fileModel)
        {
            var data = new byte[1024];

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
            Console.Clear();
            Console.WriteLine("Uploading... " + (fileModel.Packets.Where(x => x.IsSend).Sum(x => x.Size) * 100 / fileModel.Size) + "%");
            return packetNumber;
        }

        private long ContinueUploading(FileStream file)
        {
            long timeAvait;
            Console.WriteLine("File exist in current upload directory.\n" +
              "If it's not one file, after it'll be crashed. Continue uploading?[y\\n]\n");
            var time = DateTime.Now;
            var isContinue = Console.ReadLine().Contains("y");
            timeAvait = (DateTime.Now - time).Milliseconds;
            return isContinue? timeAvait : 0;
        }

        private ActionResult UploadFileUDP(string fileName)
        {
           try
            {
                var file = File.OpenRead(fileName);

                var fileModel = new FileModel()
                {
                    FileName = file.Name,
                    Size = file.Length
                };
                long packetNumber = 0;

                while (fileModel.Packets.Where(x => x.IsSend).Sum(x => x.Size) < file.Length)
                {
                    packetNumber = FirstSending(file, fileModel, packetNumber);
                }
                while (fileModel.Packets.Any())
                {
                    ResendingMissingPackets(file, fileModel);
                }
                return new ActionResult()
                {
                    FileSize = file.Length,
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
                Console.WriteLine(ex);
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
                return new ActionResult();
            }
        }

        private void ResendingMissingPackets(FileStream file, FileModel fileModel)
        {
            do
            {
                var infoCaming = new byte[1024];
                socket.ReceiveFrom(infoCaming, ref endPoint);
                var incomingString = Encoding.ASCII.GetString(infoCaming);
                if (incomingString.Contains("Correct"))
                {
                    fileModel.Packets.Clear();
                    return;
                }
                var infoPackets = incomingString.Split("|").Select(x => long.Parse(x));
                fileModel.Packets.RemoveAll(x => infoPackets.Contains(x.Number));
            } while (socketUDP.Available != 0);
            foreach (var packet in fileModel.Packets)
            {
                var data = new byte[1024];
                var info = Encoding.ASCII.GetBytes($"{packet.Number}|{packet.FilePosition}|");
                data.InsertInStartArray(info);
                file.Seek(packet.FilePosition, SeekOrigin.Begin);
                file.Read(data, info.Length, data.Length - info.Length);
                socketUDP.SendTo(data, endPoint);
                fileModel.Packets.Add(packet);
                Console.Clear();
                Console.WriteLine("Reupload UDP... " + (fileModel.Packets.Sum(x => x.Size) * 100 / fileModel.Size));
            }
        }

        private long FirstSending(FileStream file, FileModel fileModel, long packetNumber)
        {
            var data = new byte[1024];
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
            Console.Clear();
            Console.WriteLine("Upload UDP... " + (fileModel.Packets.Where(x => x.IsSend).Sum(x => x.Size) * 100 / fileModel.Size) + "%");
            return packetNumber;
        }

    }
}