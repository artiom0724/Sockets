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

        private Socket socketUDP;

        private DownloadService downloadService = new DownloadService();

        private UploadService uploadService = new UploadService();

        private EndPoint endPoint;

        public void ConnectSocket(string ip, string port)
        {
            endPoint = new IPEndPoint(IPAddress.Parse(ip), int.Parse(port));
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socket.Connect(endPoint);

            socketUDP = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Udp);
            socket.Connect(endPoint);
        }

        public void DisconnectSocket()
        {
            socket.Close();
        }

        public ActionResult DownloadFile(string fileName, string downloadingType)
        {
            var parameters = GetParameters($"client_download {fileName} {downloadingType}\r\n");
            if(parameters.Contains("Error"))
            {
                return new ActionResult();
            }
            return downloadService.DownloadFile(fileName, downloadingType, parameters, socket, socketUDP, endPoint);
        }

        public ActionResult UploadFile(string fileName, string uploadingType)
        {
            var parameters = GetParameters($"client_upload {fileName} {uploadingType}\r\n");
            if (parameters.Contains("Error"))
            {
                return new ActionResult();
            }
            return uploadService.UploadFile(fileName, uploadingType, parameters, socket, socketUDP, endPoint);
        }

        private string[] GetParameters(string command)
        {
            socket.Send(Encoding.ASCII.GetBytes(command));
            var data = new byte[1024];
            socket.Receive(data);
            var incomingString = Encoding.ASCII.GetString(data);
            var parameters = incomingString.Split('|');
            return parameters;
        }
    }
}