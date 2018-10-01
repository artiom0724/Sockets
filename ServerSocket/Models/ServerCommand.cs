using System;
using System.Collections.Generic;
using System.Text;
using static ServerSocket.Enums.Enums;

namespace ServerSocket.Models
{
    public class ServerCommand
    {
        public CommandType Type { get; set; }

        public List<string> Parameters { get; set; }
    }
}
