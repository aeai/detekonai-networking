using Detekonai.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Detekonai.Networking.Runtime.Tcp
{
    public interface ITcpConnectionManager : ILogCapable
    {
        public delegate void ClientAccepted(TcpChannel client);
        public event ClientAccepted OnClientAccepted;
        public void OnAccept(SocketAsyncEventArgs evt);
    }
}
