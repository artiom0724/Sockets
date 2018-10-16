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

        private Socket socketUDP;

        private DownloadService downloadService = new DownloadService();

        private UploadService uploadService = new UploadService();

        private DoubleEndPointModel endPointModel;

        public void ConnectSocket(string ip, string port)
        {
            endPointModel = new DoubleEndPointModel()
            {
                EndPoint = new IPEndPoint(IPAddress.Parse(ip), int.Parse(port)),
                EndPointUDP = new IPEndPoint(IPAddress.Parse(ip), int.Parse(port)+1)
            };
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socketUDP = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect(endPointModel.EndPoint);
            socketUDP.Connect(endPointModel.EndPointUDP);
        }

        public void DisconnectSocket()
        {
            socket.Shutdown(SocketShutdown.Both);
            socketUDP.Shutdown(SocketShutdown.Both);
            socket.Close();
            socketUDP.Close();
            socket.Dispose();
            socketUDP.Dispose();
        }

        public ActionResult DownloadFile(string fileName)
        {
            var parameters = GetParameters($"client_download {fileName}\r\n");
            if(parameters.Contains("Error"))
            {
                return new ActionResult();
            }
            return downloadService.DownloadFile(fileName, parameters, socket, socketUDP, endPointModel.EndPointUDP);
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
            return uploadService.UploadFile(fileName, parameters, socket, socketUDP, endPointModel.EndPointUDP);
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