using ClientSocket.Models;
using ServerSocket.Models;
using ServerSocket.Sevices;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Text;
using static ServerSocket.Enums.Enums;

namespace ServerSocket.Helpers
{
	public class CommandExecuter
	{
		private BaseCommandService baseCommandService = new BaseCommandService();

		private DownloadService downloadService = new DownloadService();

		private UploadService uploadService = new UploadService();

		public void ExecuteCommand(Socket socket, ServerCommand command, Socket socketUDPWrite, Socket socketUDPRead, TripleEndPointModel endPoint)
		{
			switch (command.Type)
			{
				case CommandType.Echo:
					baseCommandService.EchoHandler(socket, command);
					return;
				case CommandType.Time:
					baseCommandService.TimeHandler(socket);
					return;
				case CommandType.Close:
					baseCommandService.CloseHandler(socket, socketUDPRead, socketUDPWrite);
					return;
				case CommandType.Download:
					downloadService.DownloadFile(socket, endPoint.EndPoint, socketUDPWrite, command, ProtocolType.Tcp);
					return;
				case CommandType.Upload:
					uploadService.UploadFile(socket, endPoint.EndPoint, socketUDPRead, command, ProtocolType.Tcp);
					return;
				case CommandType.DownloadUDP:
					downloadService.DownloadFile(socket, endPoint.EndPointUDPWrite, socketUDPWrite, command, ProtocolType.Udp);
					return;
				case CommandType.UploadUDP:
					uploadService.UploadFile(socket, endPoint.EndPointUDPRead, socketUDPRead, command, ProtocolType.Udp);
					return;
				case CommandType.Unknown:
					baseCommandService.UnknownHandler(socket);
					return;
			}
		}

		public bool ContinueExecuteCommand(MultiSocketModel tempSocket, ServerCommand command)
		{
			switch (command.Type)
			{
				case CommandType.DownloadUDP:
					return downloadService.ContinueExecute(tempSocket.handler, tempSocket.EndPointUDPWrite, tempSocket.EndPointUDPRead, tempSocket.socketUDP, tempSocket.socketUDPRead, command, ProtocolType.Udp);
				case CommandType.UploadUDP:
					return uploadService.ContinueExecute(tempSocket.handler, tempSocket.EndPointUDPRead, tempSocket.EndPointUDPWrite, tempSocket.socketUDPRead, tempSocket.socketUDP, command, ProtocolType.Udp);
			}
			return false;
		}

		public bool ContinueExecuteCommandThreading(Socket socket, ServerCommand command, Socket socketUDPWrite, Socket socketUDPRead, TripleEndPointModel endPoint)
		{
			switch (command.Type)
			{
				case CommandType.DownloadUDP:
					while (true)
					{
						Console.WriteLine(socket.Connected);
						if (downloadService.ContinueExecute(socket, endPoint.EndPointUDPWrite, endPoint.EndPointUDPRead, socketUDPWrite, socketUDPRead, command, ProtocolType.Udp))
						{
							break;
						}
					};
					break;
				case CommandType.UploadUDP:
					while (true)
					{
						if (uploadService.ContinueExecute(socket, endPoint.EndPointUDPRead, endPoint.EndPointUDPWrite, socketUDPRead, socketUDPWrite, command, ProtocolType.Udp))
						{
							break;
						}
					};
					break;
			}
			return false;
		}
	}
}
