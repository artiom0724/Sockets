using ServerSocket.Helpers;
using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;

namespace ServerSocket
{
    public class Server
    {
        private bool running = false;

        private Thread workerThread;

        private SocketWorker socketWorker = new SocketWorker();

        public bool StartServer(int portNum)
        {
            if (running)
            {
                return false;
            }
            Console.Write("Input <[IP-address] [port]> for start: ");
            var inputData = Console.ReadLine();
            var startParameters = inputData.Split(' ');
            var ip = IPAddress.Parse(startParameters[0]);
            var port = int.Parse(startParameters[1]);
            var endPoint = new IPEndPoint(ip, port);
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
