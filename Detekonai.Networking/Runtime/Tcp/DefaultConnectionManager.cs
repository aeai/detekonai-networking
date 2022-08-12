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

        public ILogger Logger { get; set; } = null;

        public DefaultConnectionManager(ICommChannelFactory<TcpChannel, IConnectionData> factory)
        {
            this.factory = factory;
        }

        public void OnAccept(IConnectionData evt)
        {
            TcpChannel ch = factory.CreateFrom(evt);
            if (ch != null)
            {
                int id = Interlocked.Increment(ref counter);
                ch.Name = $"Channel-{id}";
                OnClientAccepted?.Invoke(ch);
                Logger?.Log(this, $"TCP Channel-{id} assigned to {((IPEndPoint)evt.Sock.RemoteEndPoint).Address}:{((IPEndPoint)evt.Sock.RemoteEndPoint).Port}", ILogger.LogLevel.Verbose);
            }
            else
            {
                Logger?.Log(this, $"Failed to initialize channel! ", ILogger.LogLevel.Error);
                evt.Sock.Shutdown(SocketShutdown.Both);
                evt.Sock.Close();
            }
        }

    }
}
