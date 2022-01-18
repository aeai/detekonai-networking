using Detekonai.Core;
using Detekonai.Networking.Runtime.AsyncEvent;
using Detekonai.Networking.Runtime.Strategy;

namespace Detekonai.Networking.Runtime.Tcp
{
    public class ConnectionManagedTcpChannelFactory : ICommChannelFactory<TcpChannel, IConnectionData>
    {
        private readonly SocketAsyncEventArgsPool evtPool;
        private readonly IAsyncEventCommStrategy strategy;
        private readonly BinaryBlobPool blobPool;
        public ConnectionManagedTcpChannelFactory(SocketAsyncEventArgsPool evtPool, IAsyncEventCommStrategy strategy, BinaryBlobPool blobPool)
        {
            this.evtPool = evtPool;
            this.strategy = strategy;
            this.blobPool = blobPool;
        }
        public TcpChannel Create()
        {
            TcpChannel channel = new TcpChannel(strategy, evtPool, blobPool);
            return channel;
        }

        public TcpChannel CreateFrom(IConnectionData data)
        {
            TcpChannel channel = new TcpChannel(strategy, evtPool, blobPool);
            channel.AssignSocket(data.Sock);
            return channel;
        }
    }
}
