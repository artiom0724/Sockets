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

        private Socket socketUDPWrite;

        private Socket socketUDPRead;

        private DownloadService downloadService = new DownloadService();

        private UploadService uploadService = new UploadService();

        private TripleEndPointModel endPointModel;

        public void ConnectSocket(string ip, string port)
        {
            if (socket == null && socketUDPWrite == null && socketUDPRead == null)
            {
                endPointModel = new TripleEndPointModel()
                {
                    EndPoint = new IPEndPoint(IPAddress.Parse(ip), int.Parse(port)),
                };
                socket = CreateSocket(ProtocolType.Tcp, endPointModel.EndPoint);
                endPointModel.EndPointUDPRead = new IPEndPoint(((IPEndPoint)(socket.RemoteEndPoint)).Address, (((IPEndPoint)(socket.RemoteEndPoint)).Port + 2));
                endPointModel.EndPointUDPWrite = new IPEndPoint(((IPEndPoint)(socket.LocalEndPoint)).Address, (((IPEndPoint)(socket.LocalEndPoint)).Port + 1));
                socketUDPWrite = CreateSocket(ProtocolType.Udp);
                socketUDPRead = CreateSocket(ProtocolType.Udp, endPointModel.EndPointUDPRead);
            }
        }

        private Socket CreateSocket(ProtocolType type, EndPoint endPoint = null)
        {
            var newSocket = new Socket(AddressFamily.InterNetwork, type == ProtocolType.Udp ? SocketType.Dgram : SocketType.Stream, type);
            if (endPoint != null && ProtocolType.Udp == type)
            {
                newSocket.Bind(endPoint);
            }
            if (type == ProtocolType.Tcp)
            {
                newSocket.Connect(endPoint);
            }

            return newSocket;
        }

        public void DisconnectSocket()
        {
            socket.Shutdown(SocketShutdown.Both);
            socketUDPWrite.Shutdown(SocketShutdown.Both);
            socketUDPRead.Shutdown(SocketShutdown.Both);
            socket.Close();
            socketUDPWrite.Close();
            socketUDPRead.Close();
            socket = null;
            socketUDPWrite = null;
            socketUDPRead = null;
        }

        public ActionResult DownloadFile(string fileName, ProtocolType type)
        {
            var parameterType = type == ProtocolType.Udp ? "_udp" : string.Empty;
            var parameters = GetParameters($"client_download{parameterType} {fileName}\r\n");
            if(parameters.Contains("Error"))
            {
                return new ActionResult();
            }
            var returning = downloadService.DownloadFile(fileName, parameters, socket, socketUDPRead, endPointModel.EndPointUDPRead, type);
            return returning;
        }

        public ActionResult UploadFile(string fileName, ProtocolType type)
        {
            var file = File.OpenRead(fileName);
            var parameterType = type == ProtocolType.Udp ? "_udp" : string.Empty;
            var parameters = GetParameters($"client_upload{parameterType} {fileName} {file.Length}\r\n", type);
            file.Close();
            if (parameters.Contains("Error"))
            {
                return new ActionResult();
            }
            return uploadService.UploadFile(fileName, parameters, socket, socketUDPWrite, endPointModel.EndPointUDPWrite, type);
        }

        private string[] GetParameters(string command, ProtocolType type = ProtocolType.Tcp)
        {
            socket.Send(Encoding.ASCII.GetBytes(command));
            var data = new byte[4096];

            if (type == ProtocolType.Tcp)
            {
                socket.Receive(data);
                var incomingString = Encoding.ASCII.GetString(data);
                var parameters = incomingString.Split('|');
                return parameters;
            }
            return new string[1];
        }
    }
}