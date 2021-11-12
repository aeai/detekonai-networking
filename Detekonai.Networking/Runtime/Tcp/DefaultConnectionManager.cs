using Detekonai.Core.Common;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace Detekonai.Networking.Runtime.Tcp
{
    public class DefaultConnectionManager : ITcpConnectionManager
    {
        private readonly ICommChannelFactory<TcpChannel> factory;

        public event ITcpConnectionManager.ClientAccepted OnClientAccepted;

        private int counter = 0;

        public ILogConnector Logger { get; set; } = null;

        public DefaultConnectionManager(ICommChannelFactory<TcpChannel> factory)
        {
            this.factory = factory;
        }

        public void OnAccept(Socket evt)
        {
            int id = Interlocked.Increment(ref counter);
            TcpChannel ch = factory.Create();
            ch.Name = $"Channel-{id}";
            ch.AssignSocket(evt);
            OnClientAccepted?.Invoke(ch);
            Logger?.Log(this, $"TCP Channel-{id} assigned to {((IPEndPoint)evt.RemoteEndPoint).Address}:{((IPEndPoint)evt.RemoteEndPoint).Port}", ILogConnector.LogLevel.Verbose);
        }

    }
}
