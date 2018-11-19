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

        private Socket socketUDP;

            private EndPoint endPoint;

            private EndPoint ipClient;
            private EndPoint savedClient;
            private List<FileModel> fileModels = new List<FileModel>();
            private FileModel udpModel;

        public void UploadFile(Socket socket, EndPoint endPoint, Socket socketUDP, ServerCommand command, ProtocolType type)
        {
            this.socket = socket;
            this.socketUDP = socketUDP;
            this.ipClient = socket.RemoteEndPoint;
            this.endPoint = endPoint;
            switch (type)
            {
                case ProtocolType.Tcp:
                    UploadFileTCP(command);
                    break;
                case ProtocolType.Udp:
                    UploadFileUDP(command);
                    break;
            }
			udpModel = null;
			savedClient = socket.RemoteEndPoint;
        }

        private void UploadFileTCP(ServerCommand command)
        {
            FileStream file = null;
            FileModel model;
            var notSameClient = ipClient?.ToString().Split(":").First() != savedClient?.ToString().Split(":").First();
            try
			{
				if (notSameClient)
				{
					fileModels.Clear();
				}
				CheckFileAndFileParameters(command, out file, out model);
				var streamTCPWriter = new PacketWriter();

				while (model.Packets.Where(x => x.IsCame).Sum(x => x.Size) < model.Size)
				{
					GettingProcess(file, streamTCPWriter, model);
				}
				fileModels.RemoveAll(x => x.FileName == model.FileName);
				file.Close();
			}
			catch (Exception exc)
            {
                if (file != null)
                {
                    file.Close();
                }
                Console.WriteLine(exc.Message);
                Console.WriteLine(exc.StackTrace);
            }
        }

		private void CheckFileAndFileParameters(ServerCommand command, out FileStream file, out FileModel model)
		{
			if (!fileModels.Where(m => m.FileName == command.Parameters.First()).Any())
			{
				model = new FileModel()
				{
					FileName = command.Parameters.First(),
					Size = long.Parse(command.Parameters.Last())
				};
				fileModels.Add(model);
				File.Delete(command.Parameters.First());
			}
			file = File.OpenWrite(command.Parameters.First());
			model = fileModels.Where(m => m.FileName == command.Parameters.First()).First();
			var data = new byte[4096];
			if (file.Length > 0)
			{
				socket.Send((new byte[4096]).InsertInStartArray(Encoding.ASCII.GetBytes($"{file.Length}|")));
				var camingBytes = 0;
				using (var stream = new PacketWriter())
				{
					while (camingBytes < 4096)
					{
						var tempCamingBytes = socket.Receive(data);
						camingBytes += tempCamingBytes;
						stream.Write(data.SubArray(0, tempCamingBytes));
					}
				}
				file = CheckFileSize(command, file, model, data);
			}
			else
			{
				socket.Send((new byte[4096]).InsertInStartArray(Encoding.ASCII.GetBytes("0|")));
			}
		}

		private static FileStream CheckFileSize(ServerCommand command, FileStream file, FileModel model, byte[] data)
		{
			if (Encoding.ASCII.GetString(data).Split("|").First().Contains("continue"))
			{
				file.Seek(file.Length, SeekOrigin.Begin);
				model.Packets.Clear();
				model.Packets.Add(new PacketModel()
				{
					Number = 0,
					Size = file.Length,
					IsCame = true
				});
			}
			else
			{
				var tempFileName = file.Name;
				file.Close();
				File.Delete(tempFileName);
				file = File.OpenWrite(command.Parameters.First());
			}

			return file;
		}

		private bool GettingProcess(FileStream file, PacketWriter streamTCPWriter, FileModel model)
        {
			var camingBytes = 0;
			while (streamTCPWriter.ToByteArray().Length < 4096)
			{
				var tempdata = new byte[4096];
				var tempCamingBytes = socket.Receive(tempdata);
				camingBytes += tempCamingBytes;
				streamTCPWriter.Write(tempdata.SubArray(0, tempCamingBytes));
			}
			var tempData = streamTCPWriter.ToByteArray();
			streamTCPWriter.Clear();
			if (tempData.Length > 4096)
			{
				streamTCPWriter.Write(tempData.SubArray(4096, tempData.Length - 4096));
			}
			var data = tempData.SubArray(0, 4096);
            long packetNumber, filePosition;
            byte[] writedData;
			using (var stream = new PacketReader(data))
			{
				packetNumber = stream.ReadInt64();
				filePosition = stream.ReadInt64();
				writedData = stream.ReadBytes(data.Length - 2 * sizeof(long));
			}
            if (model.Size - file.Length < writedData.Length)
            {
                writedData = writedData.SubArray(0, model.Size - file.Length);
            }
			file.Seek(filePosition, SeekOrigin.Begin);
            file.Write(writedData, 0, writedData.Length);
            model.Packets.Add(new PacketModel()
            {
                Size = writedData.Length,
                IsCame = true,
                Number = packetNumber
            });
            Console.Write("\rGetting... " + (model.Packets.Where(x => x.IsCame).Sum(x => x.Size) * 100 / model.Size) + "%");
			return true;
        }

        private void UploadFileUDP(ServerCommand command)
        {
            FileStream file = null;
            try
            {
                file = File.OpenWrite(command.Parameters.First());
                udpModel = new FileModel()
                {
                    FileName = file.Name,
                    Size = long.Parse(command.Parameters[1])
                };
                if (file.Length > 0)
                {
                    var fileName = file.Name;
                    file.Close();
                    File.Delete(fileName);
                    file = File.OpenWrite(fileName);
                }
                long gettedPacketsCount = 0;
                var errors = 0;
                while (file.Length < udpModel.Size)
                {
                    do
                    {
                        if (FirstDataGetting(file))
                        {
                            gettedPacketsCount++;
                        }
                        else
                        {
                            errors++;
                        }
                    } while (udpModel.Packets.Sum(x => x.Size) < udpModel.Size && errors != 5 && gettedPacketsCount < 16);
                    while (udpModel.Size < udpModel.Packets.Sum(x => x.Size) && gettedPacketsCount < 16)
                    {
                        RegettingMissingPackets(file, ref gettedPacketsCount);
                    }
                    gettedPacketsCount = 0;
                    socket.Send(Encoding.ASCII.GetBytes("Correct"));
                }
            }
            catch (Exception exc)
            {
                if (file != null)
                {
                    file.Close();
                }
                Console.WriteLine(exc.Message);
                Console.WriteLine(exc.StackTrace);
            }
            file.Close();
        }

        private void RegettingMissingPackets(FileStream file, ref long gettedPacketsCount)
        {
            var camingPackets = udpModel.Packets.TakeLast(16).Select(x => x.Number).ToList();
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
                GettingMissingPackets(file);
                gettedPacketsCount++;
            } while (gettedPacketsCount < 16);
        }

        private void GettingMissingPackets(FileStream file)
        {
            DataGetting(file);
            Console.Write("\rRegetting... " + (udpModel.Packets.Where(x => x.IsCame).Sum(x => x.Size) * 100 / udpModel.Size) + "%");
        }

        private void SendCamingPackagesNumbers(List<long> camingPackets)
        {
            int offset = 0;
            using (var stream = new PacketWriter())
            {
                while (camingPackets.Any())
                {
                    var number = camingPackets.First();
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

        private bool FirstDataGetting(FileStream file)
        {
            var receive = DataGetting(file);
            Console.Write("\rGetting... " + (udpModel.Packets.Where(x => x.IsCame).Sum(x => x.Size) * 100 / udpModel.Size) + "%");
            return receive;
        }

        private bool DataGetting(FileStream file)
        {
            var data = new byte[4096];
            var receive = socketUDP.ReceiveFrom(data, ref endPoint);
            if (receive == 0)
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
            if (udpModel.Size - file.Length < writedData.Length)
            {
                writedData = writedData.SubArray(0, udpModel.Size - file.Length);
            }
            file.Seek(filePosition, SeekOrigin.Begin);
            file.Write(writedData, 0, writedData.Length);
            udpModel.Packets.Add(new PacketModel()
            {
                Size = writedData.Length,
                IsCame = true,
                Number = packetNumber,
                FilePosition = filePosition
            });
            return true;
        }

        private void MinimizeList()
        {

        }
    }
}
