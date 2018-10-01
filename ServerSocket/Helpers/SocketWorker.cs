using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ServerSocket.Helpers
{
    public class SocketWorker
    {
        private Socket socket;

        private Socket socketUDP;

        private CommandParser commandParser = new CommandParser();

        private CommandExecuter commandExecuter = new CommandExecuter();

        private EndPoint endPoint;

        public void AwaitCommand(object data)
        {
            endPoint = (EndPoint)data;
            while (true)
            {
                try
                {
                    CreateSocket();
                    CreateUDPSocket();
                    MonitorPort();
                    socket.Close();
                    socketUDP.Close();
                    Console.WriteLine("Disconnect");
                }
                catch (Exception exc)
                {
                    socket.Close();
                    socketUDP.Close();
                    Console.WriteLine(exc.Message);
                }
            }
        }

        private void MonitorPort()
        {
            while (true)
            {
                Socket handler = socket.Accept();
                Socket handlerUDP = socket.Accept();
                while (handler.Connected)
                {
                    StringBuilder builder = new StringBuilder();
                    int bytes = 0;
                    byte[] socketData = new byte[256];

                    do
                    {
                        bytes = handler.Receive(socketData);
                        builder.Append(Encoding.ASCII.GetString(socketData, 0, bytes));
                        if (builder.ToString().Contains("\r\n"))
                            break;
                    }
                    while (handler.Connected && !builder.ToString().Contains("\r\n"));
                    var commandString = builder.ToString();
                    commandExecuter.ExecuteCommand(handler, handlerUDP, commandParser.ParseCommand(commandString), endPoint);

                    if (!handler.Connected)
                    {
                        break;
                    }
                }
            }
        }

        private void CreateSocket()
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Bind(endPoint);
            socket.Listen(10);
        }

        private void CreateUDPSocket()
        {
            socketUDP = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Udp);
            socketUDP.Bind(endPoint);
            socketUDP.Listen(10);
        }
    }
}
