using ServerSocket.Helpers;
using System;

namespace PingSocket
{
	class Program
	{
		static void Main(string[] args)
		{
			try
			{
				var command = new CommandParser().ParseCommand(Console.ReadLine());

				PingCommandExecuter.Execute(command);
			}
			catch (Exception e)
			{
				Console.WriteLine(e);
			}
		}
	}
}
