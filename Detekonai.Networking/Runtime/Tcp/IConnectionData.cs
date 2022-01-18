using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Detekonai.Networking.Runtime.Tcp
{
    public interface IConnectionData
    {
        Socket Sock { get; }
    }
}
