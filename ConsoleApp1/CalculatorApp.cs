using MPICalculator.Helpers;
using MPICalculator.SevenMethods;
using ServerSocket.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static ServerSocket.Enums.Enums;

namespace MPICalculator
{
	public class CalculatorApp
	{
		public void Start(ServerCommand command)
		{
			switch(command.Type)
			{
				case CommandType.Blocked:
					var method = new BlockedMethod();
					method.SetParams(int.Parse(command.Parameters[1]), int.Parse(command.Parameters[2]));
					method.Execute(command.Parameters);
					break;
				case CommandType.GetMatrixs:
					new MatrixHelper().Start(int.Parse(command.Parameters[1]), int.Parse(command.Parameters[2]));
					break;
				case CommandType.Unblocked:
					var umethod = new UnblockedMedhod();
					umethod.SetParams(int.Parse(command.Parameters[1]), int.Parse(command.Parameters[2]));
					umethod.Execute(command.Parameters);
					break;
				case CommandType.Group:
					var gmethod = new GroupMethod();
					gmethod.SetParams(int.Parse(command.Parameters[1]), int.Parse(command.Parameters[2]), int.Parse(command.Parameters[3]));
					gmethod.Execute(command.Parameters);
					break;
				case CommandType.Unknown:
					break;
			}
		}
	}
}
