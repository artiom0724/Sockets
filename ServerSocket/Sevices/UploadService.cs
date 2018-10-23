﻿using System;
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
            switch (type)
            {
                case ProtocolType.Tcp:
                    UploadFileTCP(command);
                    break;
                case ProtocolType.Udp:
                    UploadFileUDP(command);
                    break;
            }
            savedClient = socket.RemoteEndPoint;
        }

        private void UploadFileTCP(ServerCommand command)
        {
            FileStream file = null;
            Console.Clear();
            FileModel model;
            var notSameClient = ipClient?.ToString().Split(":").First() != savedClient?.ToString().Split(":").First();
            try
            {
                if (notSameClient)
                {
                    fileModels.Clear();
                }
                if(!fileModels.Where(m=>m.FileName==command.Parameters.First()).Any())
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
                model = fileModels.Where(m=>m.FileName == command.Parameters.First()).First();
                var data = new byte[4096];
                if (file.Length > 0)
                {
                    socket.Send(Encoding.ASCII.GetBytes($"{file.Length}|"));
                    socket.Receive(data);
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
                        File.Delete(file.Name);
                        file = File.OpenWrite(command.Parameters.First());
                    }
                }
                else
                {
                    socket.Send(Encoding.ASCII.GetBytes("0|"));
                }

                while (model.Packets.Where(x => x.IsCame).Sum(x => x.Size) < model.Size)
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
            socket.Receive(data);
            var model = fileModels.Where(m=>m.FileName == Path.GetFileName(file.Name)).First();
            var incomingString = Encoding.ASCII.GetString(data);
            var packetParameters = incomingString.Split('|');
            var parametersSize = Encoding.ASCII.GetBytes($"{packetParameters[0]}|{packetParameters[1]}|").Count();
            var writedData = data.SubArray(parametersSize, data.Length - parametersSize);
            if (model.Size - file.Length < writedData.Length)
            {
                writedData = writedData.SubArray(0, model.Size - file.Length);
            }
            file.Write(writedData, 0, writedData.Length);
            model.Packets.Add(new PacketModel()
            {
                Size = writedData.Length,
                IsCame = true,
                Number = long.Parse(packetParameters[0])
            });
            Console.Write("\rGetting... " + (model.Packets.Where(x => x.IsCame).Sum(x => x.Size) * 100 / model.Size) + "%");
        }

        private void UploadFileUDP(ServerCommand command)
        {
            Console.Clear();
            FileStream file = null;
            try
            {
                file = File.OpenWrite(command.Parameters.First());
                udpModel = new FileModel()
                {
                    FileName = file.Name,
                    Size = int.Parse(command.Parameters[1])
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
                    } while (udpModel.Packets.Sum(x => x.Size) < udpModel.Size);
                    while (udpModel.Size < udpModel.Packets.Sum(x => x.Size) && gettedPacketsCount < 64)
                    {
                        RegettingMissingPackets(file, ref gettedPacketsCount);
                    }
                    gettedPacketsCount = 0;
                    socketUDP.SendTo(Encoding.ASCII.GetBytes("Correct"), endPoint);
                }
            }
            catch (Exception exc)
            {
                if (file != null)
                {
                    file.Close();
                }
                Console.WriteLine(exc.Message);
            }
            file.Close();
        }

        private void RegettingMissingPackets(FileStream file, ref long gettedPacketsCount)
        {
            var camingPackets = udpModel.Packets.Select(x => Encoding.ASCII.GetBytes($"{x.Number.ToString()}|")).ToList();
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
            Console.Write("\rRegetting... " + (udpModel.Packets.Where(x => x.IsCame).Sum(x => x.Size) * 100 / udpModel.Size) + "%");
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
            Console.Write("\rGetting... " + (udpModel.Packets.Where(x => x.IsCame).Sum(x => x.Size) * 100 / udpModel.Size) + "%");
        }

        private void DataGetting(FileStream file)
        {
            var data = new byte[4096];
            socketUDP.ReceiveFrom(data, ref endPoint);
            var incomingString = Encoding.ASCII.GetString(data);
            var packetParameters = incomingString.Split('|');
            var parametersSize = Encoding.ASCII.GetBytes($"{packetParameters[0]}|{packetParameters[1]}|").Count();
            var writedData = data.SubArray(parametersSize, data.Length - parametersSize);
            if (udpModel.Size - file.Length < writedData.Length)
            {
                writedData = writedData.SubArray(0, udpModel.Size - file.Length);
            }
            file.Seek(long.Parse(packetParameters[1]), SeekOrigin.Begin);
            file.Write(writedData, 0, writedData.Length);
            udpModel.Packets.Add(new PacketModel()
            {
                Size = writedData.Length,
                IsCame = true,
                Number = long.Parse(packetParameters[0]),
                FilePosition = long.Parse(packetParameters[1])
            });
        }
    }
}
