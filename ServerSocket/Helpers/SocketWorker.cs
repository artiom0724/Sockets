using ClientSocket.Models;
using System;
using System.Collections.Generic;
using System.Linq;
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
                    MonitorPortAsync();
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
				socketUDPWrite.Shutdown(SocketShutdown.Both);
			    socketUDPWrite.Close();
                socketUDPWrite = null;
            }
            if (socketUDPRead != null)
            {
				socketUDPRead.Shutdown(SocketShutdown.Both);
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

		private Dictionary<Socket, Socket> sockets = new Dictionary<Socket, Socket>();

		bool IsNewSocket;
		Socket handler;

		public void AcceptCallback(IAsyncResult ar)
		{
			IsNewSocket = true;
			var listener = (Socket)ar.AsyncState;
			handler = listener.EndAccept(ar);
		}

		public void MonitorPortAsync()
		{
			socket.ReceiveTimeout = 3000;
			socket.SendTimeout = 3000;

			while (true)
			{
				try
				{
					var test = socket.BeginAccept(new AsyncCallback(AcceptCallback), socket);
					if (IsNewSocket)
					{
						IsNewSocket = false;
						handler.ReceiveTimeout = 3000;
						UpdateSocketsData();
					}
					while (handler.Connected)
					{
						StringBuilder builder = new StringBuilder();
						int bytes = 0;
						byte[] socketData = new byte[256];

						do
						{
							foreach (var tempSocket in sockets)
							{
								try
								{
									bytes = tempSocket.Key.Receive(socketData);
									handler = tempSocket.Key;
									socketUDPWrite = tempSocket.Value;
									break;
								}
								catch (Exception exc)
								{
									if (!tempSocket.Key.Connected)
									{
										sockets.Remove(tempSocket.Key);
									}
									throw;
								}
							}
							if (bytes > 0)
							{
								builder.Append(Encoding.ASCII.GetString(socketData, 0, bytes));
								bytes = 0;
							}
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
				catch (Exception exc)
				{

				}
			}
		}

		private void UpdateSocketsData()
		{
			if (handler.Connected)
			{
				UpdateUdpWriteSocket();
				if (socketUDPRead != null)
				{
					ConnectUdpSockets(handler);
				}

				if (!sockets.Select(x => x.Key.RemoteEndPoint).Contains(handler.RemoteEndPoint))
				{
					sockets.Add(handler, socketUDPWrite);
				}
				Console.WriteLine($"Connected client with address {handler.RemoteEndPoint.ToString()}");
			}
			else if (socketUDPWrite != null && socketUDPRead != null)
			{
				DissconnectSockets();
			}
		}

		private void UpdateUdpWriteSocket()
		{
			endPointModel.EndPointUDPRead = new IPEndPoint(((IPEndPoint)(handler.LocalEndPoint)).Address, (((IPEndPoint)(handler.LocalEndPoint)).Port + 1));
			socketUDPRead = CreateSocket(ProtocolType.Udp, endPointModel.EndPointUDPRead);
		}

		private void ConnectUdpSockets(Socket handler)
        {
           

			endPointModel.EndPointUDPWrite = new IPEndPoint(((IPEndPoint)(handler.RemoteEndPoint)).Address, (((IPEndPoint)(handler.RemoteEndPoint)).Port + 2));
			socketUDPWrite = CreateSocket(ProtocolType.Udp);
        }
		private void DissconnectSockets()
		{
			socketUDPWrite.Shutdown(SocketShutdown.Both);
			socketUDPRead.Shutdown(SocketShutdown.Both);
			socketUDPWrite.Close();
			socketUDPRead.Close();
			socketUDPWrite = null;
			socketUDPRead = null;
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
