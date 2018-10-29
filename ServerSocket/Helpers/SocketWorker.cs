﻿using ClientSocket.Models;
using System;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ServerSocket.Helpers
{
    public class SocketWorker
    {
        private Socket socket;

        private Socket socketUDPWrite;

        private Socket socketUDPRead;

        private CommandParser commandParser = new CommandParser();

        private CommandExecuter commandExecuter = new CommandExecuter();

        private TripleEndPointModel endPointModel;

        public void AwaitCommand(object data)
        {
            endPointModel = (TripleEndPointModel)data;
            while (true)
            {
                try
                {
                    socket = CreateSocket(ProtocolType.Tcp, endPointModel.EndPoint);
                    endPointModel.EndPointUDPRead = new IPEndPoint(((IPEndPoint)(socket.LocalEndPoint)).Address, (((IPEndPoint)(socket.LocalEndPoint)).Port + 1));
                    endPointModel.EndPointUDPWrite = new IPEndPoint(((IPEndPoint)(socket.LocalEndPoint)).Address, (((IPEndPoint)(socket.LocalEndPoint)).Port + 2));
                    socketUDPWrite = CreateSocket(ProtocolType.Udp, endPointModel.EndPointUDPWrite);
                    socketUDPRead = CreateSocket(ProtocolType.Udp, endPointModel.EndPointUDPRead);
                    MonitorPort();
                    socket.Close();
                    socketUDPWrite.Close();
                    socketUDPRead.Close();
                    Console.WriteLine("Disconnect");
                }
                catch (Exception exc)
                {
                    if (socket != null)
                    {
                        socket.Close();
                    }
                    if(socketUDPWrite != null)
                    {
                        socketUDPWrite.Close();
                    }
                    if (socketUDPWrite != null)
                    {
                        socketUDPRead.Close();
                    }
                    Console.WriteLine(exc.Message);
                    Console.WriteLine(exc.StackTrace);
                }
            }
        }

        private void MonitorPort()
        {
            while (true)
            {
                Socket handler = socket.Accept();
                Console.WriteLine ($"Connected client with address {handler.RemoteEndPoint.ToString()}");
                while (handler.Connected)
                {
                    StringBuilder builder = new StringBuilder();
                    int bytes = 0;
                    byte[] socketData = new byte[256];

                    do
                    {
                        if(handler.Poll(20000, SelectMode.SelectRead))
                        {
                            throw new SocketException((int)SocketError.ConnectionReset);
                        }
                        bytes = handler.Receive(socketData);
                        builder.Append(Encoding.ASCII.GetString(socketData, 0, bytes));
                        if (builder.ToString().Contains("\r\n"))
                            break;
                    }
                    while (handler.Connected && !builder.ToString().Contains("\r\n"));
                    var commandString = builder.ToString();
                    
                    commandExecuter.ExecuteCommand(handler, commandParser.ParseCommand(commandString), socketUDPWrite, socketUDPRead, endPointModel);
                    if (!handler.Connected)
                    {
                        break;
                    }
                }
            }
        }

        private Socket CreateSocket(ProtocolType type, EndPoint endPoint = null)
        {
            var newSocket = new Socket(AddressFamily.InterNetwork, type == ProtocolType.Udp? SocketType.Dgram : SocketType.Stream, type);
            if(endPoint != null)
            {
                newSocket.Bind(endPoint);
            }
            if (type == ProtocolType.Tcp)
            {             
                newSocket.Listen(10);
            }

            return newSocket;
        }
    }
}
