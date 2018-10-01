using System;
using System.Collections.Generic;
using System.Text;

namespace ServerSocket.Enums
{
    public static class Enums
    {
        public enum CommandType
        {
            Echo,
            Time,
            Close,
            Download,
            Unknown,
            Upload
        }
    }
}
