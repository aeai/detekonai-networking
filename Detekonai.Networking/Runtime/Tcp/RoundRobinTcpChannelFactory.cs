using Detekonai.Core;
using Detekonai.Networking.Runtime.AsyncEvent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Detekonai.Networking.Runtime.Tcp
{
    public class RoundRobinTcpChannelFactory : ICommChannelFactory<TcpChannel>
    {
            private readonly SocketAsyncEventArgsPool evtPool;
            private readonly List<IAsyncEventHandlingStrategy> strategies;
            private readonly BinaryBlobPool blobPool;

            private int currentStrategy = 0;
     
            public RoundRobinTcpChannelFactory(SocketAsyncEventArgsPool evtPool, List<IAsyncEventHandlingStrategy> strategies, BinaryBlobPool blobPool)
            {
                this.evtPool = evtPool;
                this.strategies = strategies;
                this.blobPool = blobPool;
            }

            public TcpChannel Create()
            {
                TcpChannel res = new TcpChannel(strategies[currentStrategy], evtPool, blobPool);
                currentStrategy = (currentStrategy+1) % strategies.Count;
                return res;
            }
    }
}
