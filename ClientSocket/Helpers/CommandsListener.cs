using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using ClientSocket.Services;

namespace ClientSocket.Helpers
{
    public class CommandsListener
    {
        private SocketWorker socketWorker = new SocketWorker();
        public void Run()
        {
            ListenConsole();
        }

        private void ListenConsole()
        {
            while (true)
            {
                try
                {
                    var command = Console.ReadLine();
                    var splittedCommand = SplitCommandLine(command);
                    switch (splittedCommand[0])
                    {
                        case "help":
                            {
                                WriteHelpManual();
                                break;
                            }
                        case "connect":
                            {
                                ConnectMethod(splittedCommand);
                                break;
                            }
                        case "disconnect":
                            {
                                DisconnectMethod();
                                break;
                            }
                        case "download":
                            {
                                DownloadMethod(splittedCommand);
                                break;
                            }
                        case "upload":
                            {
                                UploadMethod(splittedCommand);
                                break;
                            }
                        default:
                            {
                                Console.WriteLine("Bad command \nPrint help for get manual\n");
                                break;
                            }
                    }
                }
                catch (Exception exc)
                {
                    Console.WriteLine(exc.Message);
                }
            }
        }

        private void ConnectMethod(List<string> splittedCommand)
        {
            Console.WriteLine("Connected");
            socketWorker.ConnectSocket(splittedCommand[1], splittedCommand[2]);
        }

        private void DisconnectMethod()
        {
            Console.WriteLine("Disconnected");
            socketWorker.DisconnectSocket();
        }

        private void UploadMethod(List<string> splittedCommand)
        {
            Console.WriteLine("Uploading start...");
            var time = DateTime.Now;
            var actionResult = socketWorker.UploadFile(splittedCommand[1], splittedCommand.Count >= 3 ? splittedCommand[2] : "tcp");
            if (actionResult.FileSize == 0)
            {
                Console.WriteLine("Error file. Uploading breaking");
                return;
            }
            var resultSpeed = actionResult.FileSize / ((DateTime.Now - time).Milliseconds - actionResult.TimeAwait);
            Console.WriteLine("Uploading complete\nAverage speed: "
                + resultSpeed.ToString() + " bpms");
        }

        private void DownloadMethod(List<string> splittedCommand)
        {
            Console.WriteLine("Downloading start...");
            var time = DateTime.Now;
            var actionResult = socketWorker.DownloadFile(splittedCommand[1], splittedCommand.Count >= 3 ? splittedCommand[2] : "tcp");
            if (actionResult.FileSize == 0)
            {
                Console.WriteLine("Error file. Downloading breaking");
                return;
            }
            var resultSpeed = actionResult.FileSize / ((DateTime.Now - time).Milliseconds - actionResult.TimeAwait);
            Console.WriteLine("Downloading complete\nAverage speed: "
                + resultSpeed.ToString() + " bpms");
        }

        private void WriteHelpManual()
        {
            Console.WriteLine("connect [ip*] [port*] - for connect to socket");
            Console.WriteLine("disconnect - for disconnect to socket");
            Console.WriteLine("download [filename*] [type] - download file from server");
            Console.WriteLine("upload [filename*] [type] - for connect to socket");
            Console.WriteLine("help - for getting help manual");
            Console.WriteLine("P.s. '*' - required parameter");
        }

        private static List<string> SplitCommandLine(string commandLine)
        {
            if (!commandLine.Contains(' '))
            {
                var command = new List<string>();
                command.Add(commandLine);
                return command;
            }
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