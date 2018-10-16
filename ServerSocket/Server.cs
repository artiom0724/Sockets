using ClientSocket.Models;
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
            Console.Write("* - required parameter. \nInput <[IP-address*] [port*]> for start: ");
            var inputData = Console.ReadLine();
            var startParameters = inputData.Split(' ');
            var ip = IPAddress.Parse(startParameters[0]);
            var port = int.Parse(startParameters[1]);
            socketWorker = new SocketWorker();
            workerThread = new Thread(this.MonitorPort);
            workerThread.Start(new DoubleEndPointModel() {
                EndPoint = new IPEndPoint(ip, port),
                EndPointUDP = new IPEndPoint(ip, port + 1)
            });
            running = true;
            return true;
        }

        private void MonitorPort(object data)
        {
            socketWorker.AwaitCommand(data);
        }
    }
}
