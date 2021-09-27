using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Detekonai.Core;
using Detekonai.Networking.Runtime.AsyncEvent;

namespace Detekonai.Networking.Runtime.Tcp
{
    public class SimpleTcpChannelFactory : ICommChannelFactory<TcpChannel>
    {
        private readonly SocketAsyncEventArgsPool evtPool;
        private readonly IAsyncEventHandlingStrategy strategy;
        private readonly BinaryBlobPool blobPool;

        public SimpleTcpChannelFactory(SocketAsyncEventArgsPool evtPool, IAsyncEventHandlingStrategy strategy, BinaryBlobPool blobPool)
        {
            this.evtPool = evtPool;
            this.strategy = strategy;
            this.blobPool = blobPool;
        }

        public TcpChannel Create()
        {
            return new TcpChannel(strategy, evtPool, blobPool);
        }
    }
}
