using ServerSocket.Helpers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Text;
using System.Threading;

namespace ServerSocket
{
    public class Server
    {
        private bool running = false;

        private Thread workerThread;

        private SocketWorker socketWorker;

        public bool StartServer()
        {
            if (running)
            {
                return false;
            }
            Console.Write("* - required parameter. \nInput <[IP-address*] [port*] [Protocol type*]> for start: ");
            var inputData = Console.ReadLine();
            var startParameters = inputData.Split(' ');
            var ip = IPAddress.Parse(startParameters[0]);
            var port = int.Parse(startParameters[1]);
            var endPoint = new IPEndPoint(ip, port);
            var protocolType = startParameters[2];
            socketWorker = new SocketWorker()
            {
                SelectedProtocolType = protocolType
            };
            workerThread = new Thread(this.MonitorPort);
            workerThread.Start(endPoint);
            running = true;
            return true;
        }

        private void MonitorPort(object data)
        {
            socketWorker.AwaitCommand(data);
        }
    }
}
