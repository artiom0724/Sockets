﻿using ClientSocket.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

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
                    //MonitorPort();
					MonitorPortThreads();
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

		private List<MultiSocketModel> sockets = new List<MultiSocketModel>();

		bool IsNewSocket;
		Socket handler;

		public void AcceptCallback(IAsyncResult ar)
		{
			IsNewSocket = true;
			var listener = (Socket)ar.AsyncState;
			handler = listener.EndAccept(ar);
		}

		public void MonitorPort()
		{
			while (true)
			{
				try
				{
					var test = socket.BeginAccept(new AsyncCallback(AcceptCallback), socket);
					SocketCommandsListnerRun();
				}
				catch (Exception exc)
				{

				}
			}
		}

		public void MonitorPortThreads()
		{
			while (true)
			{
				try
				{
					var threadSocket = socket.Accept();
					UpdateSocketsData(threadSocket);
					new Thread(this.MultiThreadingForSocket).Start(threadSocket);
				}
				catch (Exception exc)
				{

				}
			}
		}

		private void SocketCommandsListnerRun()
		{
			if (IsNewSocket)
			{
				IsNewSocket = false;
				handler.ReceiveTimeout = 5000;
				UpdateSocketsData();
			}
			while (handler.Connected)
			{
				StringBuilder builder = new StringBuilder();
				int bytes = 0;
				byte[] socketData = new byte[256];

				do
				{
					for (var i = 0; i < sockets.Count; i++)
					{
						var tempSocket = sockets.ElementAt(i);
						try
						{
							bytes = tempSocket.handler.Receive(socketData);
							handler = tempSocket.handler;
							socketUDPWrite = tempSocket.socketUDP;
							endPointModel.EndPointUDPWrite = tempSocket.endPointUDP;
							break;
						}
						catch (Exception exc)
						{
							if (!tempSocket.handler.Connected)
							{
								sockets.Remove(tempSocket);
							}
							if (i == sockets.Count - 1)
							{
								throw;
							}
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

		private void MultiThreadingForSocket(object data)
		{
			var threadHandler = (Socket)data;
			var threadCommandParser = new CommandParser();
			var threadCommandExecuter = new CommandExecuter();
			SocketCommandsListnerRun(threadHandler, threadCommandParser, threadCommandExecuter);
		}

		private void SocketCommandsListnerRun(Socket threadHandler, CommandParser threadCommandParser, CommandExecuter threadCommandExecuter)
		{
			var socketsModel = sockets.First(x => x.handler == threadHandler);
			var threadTripleEndPointModel = new TripleEndPointModel()
			{
				EndPoint = threadHandler.LocalEndPoint,
				EndPointUDPRead = endPointModel.EndPointUDPRead,
				EndPointUDPWrite = socketsModel.endPointUDP
			};
			threadHandler.ReceiveTimeout = 10000;
			
			while (true)
			{
				while (threadHandler.Connected)
				{
					StringBuilder builder = new StringBuilder();
					int bytes = 0;
					byte[] socketData = new byte[256];

					do
					{
						try
						{
							bytes = threadHandler.Receive(socketData);
						}
						catch (Exception exc)
						{

						}

						builder.Append(Encoding.ASCII.GetString(socketData, 0, bytes));
						bytes = 0;
						if (builder.ToString().Contains("\r\n"))
							break;
					}while (threadHandler.Connected && !builder.ToString().Contains("\r\n"));
					var commandString = builder.ToString();

					threadCommandExecuter.ExecuteCommand(threadHandler, threadCommandParser.ParseCommand(commandString), socketsModel.socketUDP, socketUDPRead, threadTripleEndPointModel);
					if (!threadHandler.Connected)
					{
						return;
					}
				}
			}
		}

		private void UpdateSocketsData(Socket threadHandler)
		{
			if (threadHandler.Connected)
			{
				UpdateUdpWriteSocket(threadHandler);
				if (socketUDPRead == null)
				{
					ConnectUdpSockets(threadHandler);
				}

				if (!sockets.Select(x => x.handler.RemoteEndPoint).Contains(threadHandler.RemoteEndPoint))
				{
					sockets.Add(new MultiSocketModel()
					{
						handler = threadHandler,
						socketUDP = socketUDPWrite,
						endPointUDP = endPointModel.EndPointUDPWrite
					});
				}
				Console.WriteLine($"Connected client with address {threadHandler.RemoteEndPoint.ToString()}");
			}
		}

		private void UpdateSocketsData()
		{
			if (handler.Connected)
			{
				UpdateUdpWriteSocket();
				if (socketUDPRead == null)
				{
					ConnectUdpSockets();
				}

				if (!sockets.Select(x => x.handler.RemoteEndPoint).Contains(handler.RemoteEndPoint))
				{
					sockets.Add(new MultiSocketModel()
					{
						handler = handler,
						socketUDP = socketUDPWrite,
						endPointUDP = endPointModel.EndPointUDPWrite
					});
				}
				Console.WriteLine($"Connected client with address {handler.RemoteEndPoint.ToString()}");
			}
		}

		private void UpdateUdpWriteSocket(Socket _handler = null)
		{
			_handler = _handler ?? handler;
			endPointModel.EndPointUDPWrite = new IPEndPoint(((IPEndPoint)(_handler.RemoteEndPoint)).Address, (((IPEndPoint)(_handler.RemoteEndPoint)).Port + 2));
			socketUDPWrite = CreateSocket(ProtocolType.Udp);
		}

		private void ConnectUdpSockets(Socket _handler = null)
        {
			_handler = _handler ?? handler;
			endPointModel.EndPointUDPRead = new IPEndPoint(((IPEndPoint)(_handler.LocalEndPoint)).Address, (((IPEndPoint)(_handler.LocalEndPoint)).Port + 1));
			socketUDPRead = CreateSocket(ProtocolType.Udp, endPointModel.EndPointUDPRead);
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
