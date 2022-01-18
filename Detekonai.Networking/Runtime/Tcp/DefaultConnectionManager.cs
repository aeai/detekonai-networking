using Detekonai.Core.Common;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Detekonai.Networking.Runtime.Tcp
{
    public class DefaultConnectionManager : ITcpConnectionManager
    {
        private readonly ICommChannelFactory<TcpChannel, IConnectionData> factory;

        public event ITcpConnectionManager.ClientAccepted OnClientAccepted;

        private int counter = 0;

        public ILogConnector Logger { get; set; } = null;

        public DefaultConnectionManager(ICommChannelFactory<TcpChannel, IConnectionData> factory)
        {
            this.factory = factory;
        }

        public void OnAccept(IConnectionData evt)
        {
            int id = Interlocked.Increment(ref counter);
            TcpChannel ch = factory.CreateFrom(evt);
            ch.Name = $"Channel-{id}";
            OnClientAccepted?.Invoke(ch);
            Logger?.Log(this, $"TCP Channel-{id} assigned to {((IPEndPoint)evt.Sock.RemoteEndPoint).Address}:{((IPEndPoint)evt.Sock.RemoteEndPoint).Port}", ILogConnector.LogLevel.Verbose);
        }

    }
}
