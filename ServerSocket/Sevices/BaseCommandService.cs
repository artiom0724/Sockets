using ServerSocket.Models;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;

namespace ServerSocket.Sevices
{
    public class BaseCommandService
    {
        public void EchoHandler(Socket socket, ServerCommand command)
        {
            var data = Encoding.ASCII.GetBytes(command.Parameters[0]);
            socket.Send(data);
        }

        public void CloseHandler(Socket socket, Socket socketUDPWrite, Socket socketUDPRead)
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
			Console.WriteLine("Disconnect");
		}

        public void TimeHandler(Socket socket)
        {
            var data = Encoding.ASCII.GetBytes($"{DateTime.Now.ToString()}\r\n");
            socket.Send(data);
        }

        public void UnknownHandler(Socket socket)
        {
            var data = Encoding.ASCII.GetBytes("Unknown command. Try again.\r\n");
            socket.Send(data);
        }
    }
}
