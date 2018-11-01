using ClientSocket.Models;
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
                    OpenSockets();
                    MonitorPort();
                    CloseSockets();
                    Console.WriteLine("\nDisconnect");
                }
                catch (Exception exc)
                {
                    ExceptionCloseSockets();
                    Console.WriteLine(exc.Message);
                    Console.WriteLine(exc.StackTrace);
                }
            }
        }

        private void ExceptionCloseSockets()
        {
            if (socket != null)
            {
                socket.Close();
                socket = null;
            }
            if (socketUDPWrite != null)
            {
                socketUDPWrite.Close();
                socketUDPWrite = null;
            }
            if (socketUDPWrite != null)
            {
                socketUDPRead.Close();
                socketUDPRead = null;
            }
        }

        private void OpenSockets()
        {
            socket = CreateSocket(ProtocolType.Tcp, endPointModel.EndPoint);
        }

        private void CloseSockets()
        {
            socket.Close();
            socketUDPWrite.Close();
            socketUDPRead.Close();
            socket = null;
            socketUDPWrite = null;
            socketUDPRead = null;
        }

        private void MonitorPort()
        {
            while (true)
            {
                Socket handler = socket.Accept();
                if (handler.Connected)
                {
                    ConnectUdpSockets(handler);
                }
                Console.WriteLine ($"Connected client with address {handler.RemoteEndPoint.ToString()}");
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
                    
                    commandExecuter.ExecuteCommand(handler, commandParser.ParseCommand(commandString), socketUDPWrite, socketUDPRead, endPointModel);
                    if (!handler.Connected)
                    {
                        break;
                    }
                }
            }
        }

        private void ConnectUdpSockets(Socket handler)
        {
            endPointModel.EndPointUDPRead = new IPEndPoint(((IPEndPoint)(handler.RemoteEndPoint)).Address, (((IPEndPoint)(handler.RemoteEndPoint)).Port + 1));
            endPointModel.EndPointUDPWrite = new IPEndPoint(((IPEndPoint)(handler.LocalEndPoint)).Address, (((IPEndPoint)(handler.LocalEndPoint)).Port + 2));
            socketUDPWrite = CreateSocket(ProtocolType.Udp);
            socketUDPRead = CreateSocket(ProtocolType.Udp, endPointModel.EndPointUDPRead);
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
