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
                EndPointUDP = new IPEndPoint(IPAddress.Parse(ip), int.Parse(port) + 1)
            };
            socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            socketUDP = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            socket.Connect(endPointModel.EndPoint);
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

        public ActionResult DownloadFile(string fileName, ProtocolType type)
        {
            socketUDP.Bind(endPointModel.EndPointUDP);

            var parameterType = type == ProtocolType.Udp ? "_udp" : string.Empty;
            var parameters = GetParameters($"client_download{parameterType} {fileName}\r\n");
            if(parameters.Contains("Error"))
            {
                return new ActionResult();
            }
            var returning = downloadService.DownloadFile(fileName, parameters, socket, socketUDP, endPointModel.EndPointUDP, type);
            socketUDP.Close();
            socketUDP = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
            return returning;
        }

        public ActionResult UploadFile(string fileName, ProtocolType type)
        {
            var file = File.OpenRead(fileName);
            var parameterType = type == ProtocolType.Udp ? "_udp" : string.Empty;
            var parameters = GetParameters($"client_upload{parameterType} {fileName} {file.Length}\r\n");
            file.Close();
            if (parameters.Contains("Error"))
            {
                return new ActionResult();
            }
            return uploadService.UploadFile(fileName, parameters, socket, socketUDP, endPointModel.EndPointUDP, type);
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