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

        public void ExecuteCommand(Socket socket, ServerCommand command, EndPoint endPoint)
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
                    baseCommandService.CloseHandler(socket);
                    return;
                case CommandType.Download:
                    downloadService.DownloadFile(socket, endPoint, command);
                    return;
                case CommandType.Upload:
                    uploadService.UploadFile(socket, endPoint, command);
                    return;
                case CommandType.Unknown:
                    baseCommandService.UnknownHandler(socket);
                    return;
            }
        }
    }
}
