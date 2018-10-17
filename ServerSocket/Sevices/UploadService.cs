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

        private FileModel fileModel;

        public void UploadFile(Socket socket, EndPoint endPoint, Socket socketUDP, ServerCommand command, ProtocolType type)
        {
            this.socket = socket;
            this.socketUDP = socketUDP;
            this.endPoint = endPoint;
            switch (type)
            {
                case ProtocolType.Tcp:
                    UploadFileTCP(command);
                    return;
                case ProtocolType.Udp:
                    UploadFileUDP(command);
                    return;
            }
            ipClient = socket.RemoteEndPoint;
        }

        private void UploadFileTCP(ServerCommand command)
        {
            FileStream file = null;
            Console.Clear();
            try
            {
                if(ipClient != endPoint)
                {
                    fileModel = new FileModel()
                    {
                        FileName = command.Parameters.First(),
                        Size = long.Parse(command.Parameters.Last())
                    };
                    File.Delete(command.Parameters.First());
                }
                file = File.OpenWrite(command.Parameters.First());
                var data = new byte[4096];
                if (file.Length > 0)
                {
                    socket.Send(Encoding.ASCII.GetBytes($"{file.Length}|"));
                    if (socket.Poll(20000, SelectMode.SelectError))
                    {
                        throw new SocketException((int)SocketError.ConnectionReset);
                    }
                    socket.Receive(data);
                    if (Encoding.ASCII.GetString(data).Split("|").First().Contains("continue"))
                    {
                        file.Seek(file.Length, SeekOrigin.Begin);
                        fileModel.Packets.Clear();
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
                else
                {
                    socket.Send(Encoding.ASCII.GetBytes("0|"));
                }

                while (fileModel.Packets.Where(x => x.IsCame).Sum(x => x.Size) < fileModel.Size)
                {
                    GettingProcess(file);
                }
                file.Close();
            }
            catch (Exception exc)
            {
                if (file != null)
                {
                    file.Close();
                }
                Console.WriteLine(exc.Message);
            }
        }

        private void GettingProcess(FileStream file)
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
                Size = writedData.Length,
                IsCame = true,
                Number = long.Parse(packetParameters[0])
            });
            Console.Write("\rGetting... " + (fileModel.Packets.Where(x => x.IsCame).Sum(x => x.Size) * 100 / fileModel.Size) + "%");
        }

        private void UploadFileUDP(ServerCommand command)
        {
            Console.Clear();
            FileStream file = null;
            try
            {
                file = File.OpenWrite(command.Parameters.First());
                fileModel = new FileModel()
                {
                    FileName = file.Name,
                    Size = int.Parse(command.Parameters.First())
                };
                if (file.Length > 0)
                {
                    File.Delete(file.Name);
                    file = File.OpenWrite(command.Parameters.First());
                }
                long gettedPacketsCount = 0;
                while (file.Length < long.Parse(command.Parameters.Last()))
                {
                    do
                    {
                        FirstDataGetting(file);
                        gettedPacketsCount++;
                    } while (fileModel.Packets.Sum(x => x.Size) < fileModel.Size);
                    while (fileModel.Size < fileModel.Packets.Sum(x => x.Size) && gettedPacketsCount < 64)
                    {
                        RegettingMissingPackets(file, ref gettedPacketsCount);
                    }
                    gettedPacketsCount = 0;
                    socket.SendTo(Encoding.ASCII.GetBytes("Correct"), endPoint);
                }
                file.Close();
            }
            catch (Exception exc)
            {
                if (file != null)
                {
                    file.Close();
                }
                Console.WriteLine(exc.Message);
            }
        }

        private void RegettingMissingPackets(FileStream file, ref long gettedPacketsCount)
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
                GettingMissingPackets(file);
                gettedPacketsCount++;
            } while (gettedPacketsCount < 64);
        }

        private void GettingMissingPackets(FileStream file)
        {
            DataGetting(file);
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
            socket.Send(data);
        }

        private void FirstDataGetting(FileStream file)
        {
            DataGetting(file);
            Console.Write("\rGetting... " + (fileModel.Packets.Where(x => x.IsCame).Sum(x => x.Size) * 100 / fileModel.Size) + "%");
        }

        private void DataGetting(FileStream file)
        {
            var data = new byte[4096];
            socketUDP.ReceiveFrom(data, ref endPoint);
            var incomingString = Encoding.ASCII.GetString(data);
            var packetParameters = incomingString.Split('|');
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
                Size = writedData.Length,
                IsCame = true,
                Number = long.Parse(packetParameters[0]),
                FilePosition = long.Parse(packetParameters[1])
            });
        }
    }
}
