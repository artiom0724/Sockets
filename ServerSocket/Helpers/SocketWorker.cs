using ClientSocket.Models;
using System;
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

        private DoubleEndPointModel endPointModel;

        public void AwaitCommand(object data)
        {
            endPointModel = (DoubleEndPointModel)data;
            while (true)
            {
                try
                {
                    socket = CreateSocket(ProtocolType.Tcp);
                    socketUDP = CreateSocket(ProtocolType.Udp);
                    MonitorPort();
                    socket.Close();
                    socketUDP.Close();
                    Console.WriteLine("Disconnect");
                }
                catch (Exception exc)
                {
                    if (socket != null)
                    {
                        socket.Close();
                    }
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
                        if(handler.Poll(20000, SelectMode.SelectError))
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
                    
                    commandExecuter.ExecuteCommand(handler, commandParser.ParseCommand(commandString), socketUDP, endPointModel.EndPointUDP);
                    if (!handler.Connected)
                    {
                        break;
                    }
                }
            }
        }

        private Socket CreateSocket(ProtocolType type)
        {
            var newSocket = new Socket(AddressFamily.InterNetwork, type == ProtocolType.Udp? SocketType.Dgram : SocketType.Stream, type);
            newSocket.Bind(type == ProtocolType.Udp? endPointModel.EndPointUDP: endPointModel.EndPoint);
            if (type == ProtocolType.Tcp)
            {
                newSocket.Listen(10);
            }
            return newSocket;
        }
    }
}
