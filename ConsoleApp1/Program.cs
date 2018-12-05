using ServerSocket.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MPICalculator
{
	class Program
	{
		static void Main(string[] args)
		{
			try
			{
				do
				{
					var fromConsole = string.Join(" ", args);
					if (fromConsole.Contains("exit"))
					{
						break;
					}
					var command = new CommandParser().ParseCommand(fromConsole);
					new CalculatorApp().Start(command);
				} while (true);
			}
			catch (Exception e)
			{
			
			}
		}
	}
}
