using System;
using System.Collections.Generic;
using System.Net;
using System.Text;

namespace ClientSocket.Models
{
    public class TripleEndPointModel
    {
        public EndPoint EndPoint { get; set; } 

        public EndPoint EndPointUDPRead { get; set; }

        public EndPoint EndPointUDPWrite { get; set; }
    }
}
