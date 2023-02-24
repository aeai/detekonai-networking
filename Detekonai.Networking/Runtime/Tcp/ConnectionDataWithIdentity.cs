using Detekonai.Networking.Runtime.Tcp;
using System.Net.Sockets;

namespace Detekonai.Networking.Tcp
{
    public class ConnectionDataWithIdentity : IConnectionData
    {
        public Socket Sock { get; }
        public string Identity { get; }

        public ConnectionDataWithIdentity(Socket sock, string identity)
        {
            Sock = sock;
            Identity = identity;
        }

    }
}
