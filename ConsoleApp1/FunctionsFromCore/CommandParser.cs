using ServerSocket.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using static ServerSocket.Enums.Enums;

namespace ServerSocket.Helpers
{
    public class CommandParser
    {
        public ServerCommand ParseCommand(string commandString)
        {
            var resCommand = new ServerCommand();
            var command = commandString.Split(' ').FirstOrDefault();
            var rest = string.Join(" ", commandString.Split(' ').Skip(1));
            resCommand.Parameters = commandString.Split(' ');
            switch (command)
            {
				case "matrixs":
					resCommand.Type = CommandType.GetMatrixs;
					return resCommand;
				case "blocked":
					resCommand.Type = CommandType.Blocked;
					return resCommand;
				case "unblocked":
					resCommand.Type = CommandType.Unblocked;
					return resCommand;
				case "group":
					resCommand.Type = CommandType.Group;
					return resCommand;
				default:
                    resCommand.Type = CommandType.Unknown;
                    return resCommand;
            }
        }

        public static List<string> SplitCommandLine(string commandLine)
        {
            var re = @"\G(""((""""|[^""])+)""|(\S+)) *";
            var ms = Regex.Matches(commandLine, re);
            var list = ms.Cast<Match>()
                         .Select(m => Regex.Replace(
                             m.Groups[2].Success
                                 ? m.Groups[2].Value
                                 : m.Groups[4].Value, @"""""", @"""")).ToList();
            return list;
        }
    }
}
