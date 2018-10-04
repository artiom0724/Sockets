using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using ClientSocket.Models;

namespace ClientSocket.Services
{
    public class SocketWorker
    {
        private Socket socket;

        private DownloadService downloadService = new DownloadService();

        private UploadService uploadService = new UploadService();

        private EndPoint endPoint;

        public void ConnectSocket(string ip, string port, string protocolType)
        {
            endPoint = new IPEndPoint(IPAddress.Parse(ip), int.Parse(port));
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, protocolType == "udp" ? ProtocolType.Udp : ProtocolType.Tcp);
            socket.Connect(endPoint);
        }

        public void DisconnectSocket()
        {
            socket.Shutdown(SocketShutdown.Both);
            socket.Close();
            socket.Dispose();
        }

        public ActionResult DownloadFile(string fileName)
        {
            var parameters = GetParameters($"client_download {fileName}\r\n");
            if(parameters.Contains("Error"))
            {
                return new ActionResult();
            }
            return downloadService.DownloadFile(fileName, parameters, socket, endPoint);
        }

        public ActionResult UploadFile(string fileName)
        {
            var file = File.OpenRead(fileName);
            var parameters = GetParameters($"client_upload {fileName} {file.Length}\r\n");
            file.Close();
            if (parameters.Contains("Error"))
            {
                return new ActionResult();
            }
            return uploadService.UploadFile(fileName, parameters, socket, endPoint);
        }

        private string[] GetParameters(string command)
        {
            socket.Send(Encoding.ASCII.GetBytes(command));
            var data = new byte[4096];
            if (socket.Poll(20000, SelectMode.SelectError))
            {
                throw new SocketException((int)SocketError.ConnectionReset);
            }
            socket.Receive(data);
            var incomingString = Encoding.ASCII.GetString(data);
            var parameters = incomingString.Split('|');
            return parameters;
        }
    }
}