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
            var command = commandString.Split(" ").FirstOrDefault();
            var rest = String.Join(" ", commandString.Split(" ").Skip(1));
            resCommand.Parameters = SplitCommandLine(rest);
            switch (command)
            {
                case "echo":
                    resCommand.Type = CommandType.Echo;
                    resCommand.Parameters.Clear();
                    resCommand.Parameters.Add(rest);
                    return resCommand;
                case "time":
                    resCommand.Type = CommandType.Time;
                    return resCommand;
                case "close":
                    resCommand.Type = CommandType.Close;
                    return resCommand;
                case "client_download":
                    resCommand.Type = CommandType.Download;
                    return resCommand;
                case "client_upload":
                    resCommand.Type = CommandType.Upload;
                    return resCommand;
                case "client_download_udp":
                    resCommand.Type = CommandType.DownloadUDP;
                    return resCommand;
                case "client_upload_udp":
                    resCommand.Type = CommandType.UploadUDP;
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
