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
    public class UploadService
    {
        private Socket socket;

        private Socket socketUDP;
        private Socket socketUDPRead;

        private EndPoint endPoint;
        private EndPoint endPointRead;

        public ActionResult UploadFile(string fileName, string[] parameters, Socket socket, Socket socketUDP, Socket socketUDPRead, EndPoint endPoint, EndPoint endPointRead, ProtocolType type)
        {
            this.socket = socket;
            this.socketUDP = socketUDP;
            this.endPoint = endPoint;
            this.endPointRead = endPointRead;
			this.socketUDPRead = socketUDPRead;
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
            long packetNumber = 0;
            var file = File.OpenRead(fileName);
            var uploaded = long.Parse(parameters[0]);
            var fileModel = new FileModel()
            {
                FileName = fileName,
                Size = file.Length
            };
            if (uploaded != 0)
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
                    socket.Send((new byte[4096]).InsertInStartArray(Encoding.ASCII.GetBytes("continue|")));
                }
                else
                {
                    socket.Send((new byte[4096]).InsertInStartArray(Encoding.ASCII.GetBytes("restore|")));
                }
            }

            while (fileModel.Packets.Where(x => x.IsSend).Sum(x => x.Size) < fileModel.Size)
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

        private long UploadingProcess(long packetNumber, FileStream file, FileModel fileModel)
        {
            var data = new byte[4096];
            using (var stream = new PacketWriter())
            {
                stream.Write(packetNumber);
                stream.Write(file.Position);
                data.InsertInStartArray(stream.ToByteArray());
            }
            var packet = new PacketModel()
            {
                Number = packetNumber,
                IsSend = true,
                Size = data.Length - 2 * sizeof(long)
            };
            packetNumber++;
            file.Read(data, 2 * sizeof(long), data.Length - 2 * sizeof(long));
            socket.Send(data);
            fileModel.Packets.Add(packet);
            var percent = ((fileModel.Packets.Where(x => x.IsSend).Sum(x => x.Size) * 100) / fileModel.Size);
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
            return isContinue ? timeAwait : 0;
        }

		private ActionResult UploadFileUDP(string fileName)
		{
			FileStream file = null;
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
				while (fileModel.Packets.Sum(x => x.Size) < file.Length)
				{
					while (fileModel.Packets.Sum(x => x.Size) < file.Length && partNumber < 16)
					{
						packetNumber = FirstSending(file, fileModel, packetNumber);
						partNumber++;
					}
					if (!(fileModel.Packets.Sum(x => x.Size) < file.Length))
					{
						ResendingMissingPackets(fileModel);
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
			catch (Exception exc)
			{
				Console.WriteLine(exc.Message + "\n" + exc.StackTrace);
				throw exc;
			}
		}

        private void ResendingMissingPackets(FileModel fileModel)
        {
            var infoCaming = new byte[4096];
            socketUDPRead.ReceiveFrom(infoCaming, ref endPointRead);
            if (!Encoding.ASCII.GetString(infoCaming).Contains("Correct"))
            {
				var missing = long.Parse(Encoding.ASCII.GetString(infoCaming));
				fileModel.Packets.RemoveAll(x => x.Number >= missing);
				fileModel.PacketCount = fileModel.Packets.Count % Constant.WindowSize;
			}
        }

        private static List<long> GetInfoPacketsNumbers(byte[] infoCaming)
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
            using (var stream = new PacketWriter())
            {
                stream.Write(packetNumber);
                stream.Write(file.Position);
                data.InsertInStartArray(stream.ToByteArray());
            }
            var packet = new PacketModel()
            {
                Number = packetNumber,
                IsSend = true,
                Size = data.Length - 2 * sizeof(long),
                FilePosition = file.Position
            };
            packetNumber++;
            file.Read(data, 2 * sizeof(long), data.Length - 2 * sizeof(long));
            socketUDP.SendTo(data, endPoint);
            fileModel.Packets.Add(packet);
            Console.Write("\rUpload UDP... " + (fileModel.Packets.Where(x => x.IsSend).Sum(x => x.Size) * 100) / fileModel.Size + "%");
            return packetNumber;
        }

    }
}