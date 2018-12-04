using PingSocket.Services;
using ServerSocket.Models;
using static ServerSocket.Enums.Enums;

namespace PingSocket
{
	public static class PingCommandExecuter
	{
		public static void Execute(ServerCommand command)
		{
			switch(command.Type)
			{
				case CommandType.Ping:
					new PingService().PingIds(command.Parameters);
					break;
				case CommandType.Tracert:
					new TracertService().TracertIds(command.Parameters);
					break;
			}
		}
	}
}