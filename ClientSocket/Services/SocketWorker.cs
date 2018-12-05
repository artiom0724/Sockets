using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using ClientSocket.Helpers;
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
                socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                endPointModel = new TripleEndPointModel()
                {
                    EndPoint = new IPEndPoint(IPAddress.Parse(ip), int.Parse(port)),
                };
                socket.Connect(endPointModel.EndPoint);
                endPointModel.EndPointUDPRead = new IPEndPoint(((IPEndPoint)(socket.LocalEndPoint)).Address, (((IPEndPoint)(socket.LocalEndPoint)).Port + 2));
                endPointModel.EndPointUDPWrite = new IPEndPoint(((IPEndPoint)(socket.RemoteEndPoint)).Address, (((IPEndPoint)(socket.LocalEndPoint)).Port + 1));
                socketUDPWrite = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socketUDPRead = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                socketUDPRead.Bind(endPointModel.EndPointUDPRead);
            }
        }

        public void DisconnectSocket()
        {
			socket.Send(Encoding.ASCII.GetBytes("close\r\n"));
            socketUDPWrite.Shutdown(SocketShutdown.Both);
            socketUDPRead.Shutdown(SocketShutdown.Both);
            socketUDPWrite.Close();
            socketUDPRead.Close();
            socketUDPWrite = null;
            socketUDPRead = null;
			socket = null;
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
			socket.ReceiveTimeout = 20000;

			socket.Send(Encoding.ASCII.GetBytes(command));
            var data = new byte[4096];

            if (type == ProtocolType.Tcp)
            {
				var receivedBytes = 0;
				using (var stream = new PacketWriter())
				{
					do
					{
						var tempReceivedBytes = socket.Receive(data);
						receivedBytes += tempReceivedBytes;
						stream.Write(data.SubArray(0, tempReceivedBytes));
					} while (receivedBytes < 4096);
					data = stream.ToByteArray();
				}
				var incomingString = Encoding.ASCII.GetString(data);
				var parameters = incomingString.Split('|');
				return parameters;
			}
            return new string[1];
        }
    }
}