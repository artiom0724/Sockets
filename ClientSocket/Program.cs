using System;
using ClientSocket.Helpers;

namespace ClientSocket
{
    class Program
    {
        static void Main(string[] args)
        {
            var commandsListener = new CommandsListener();
            commandsListener.Run();
        }
    }
}
