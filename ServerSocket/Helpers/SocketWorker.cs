using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ServerSocket.Helpers
{
    public class SocketWorker
    {
        public string SelectedProtocolType { get; set; }

        private Socket socket;

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
                    CreateSocket(SelectedProtocolType == "udp"? ProtocolType.Udp: ProtocolType.Tcp);
                    MonitorPort();
                    socket.Close();
                    Console.WriteLine("Disconnect");
                }
                catch (Exception exc)
                {
                    socket.Close();
                    Console.WriteLine(exc.Message);
                }
            }
        }

        private void MonitorPort()
        {
            while (true)
            {
                Socket handler = socket.Accept();
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
                    
                    commandExecuter.ExecuteCommand(handler, commandParser.ParseCommand(commandString), endPoint);
                    if (!handler.Connected)
                    {
                        break;
                    }
                }
            }
        }

        private void CreateSocket(ProtocolType type)
        {
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, type);
            socket.Bind(endPoint);
            socket.Listen(10);
        }
    }
}
