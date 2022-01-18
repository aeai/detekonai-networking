using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Detekonai.Networking.Runtime.Tcp
{
    public class SimpleConnectionData : IConnectionData
    {
        public Socket Sock { get; private set; }

        public SimpleConnectionData(Socket sock) 
        {
            Sock = sock;
        }
    }
}
