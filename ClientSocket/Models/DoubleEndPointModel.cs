using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace ClientSocket.Models
{
    public class DoubleEndPointModel
    {
        public EndPoint EndPoint { get; set; } 

        public EndPoint EndPointUDP { get; set; }
    }
}
