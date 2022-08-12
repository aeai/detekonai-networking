using Detekonai.Core.Common;
using System.Net.Sockets;

namespace Detekonai.Networking.Runtime.Tcp
{
    public interface ITcpConnectionManager
    {
        public delegate void ClientAccepted(TcpChannel client);
        public event ClientAccepted OnClientAccepted;
        public void OnAccept(IConnectionData data);
        public ILogger Logger { get; set; }
    }
}
