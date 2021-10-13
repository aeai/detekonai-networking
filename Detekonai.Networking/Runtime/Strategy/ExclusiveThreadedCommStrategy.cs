using Detekonai.Networking.Runtime.AsyncEvent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Detekonai.Networking.Runtime.Strategy
{
    public class ExclusiveThreadedCommStrategy : IAsyncEventCommStrategy
    {
        private int counter = 0;
        public void EnqueueEvent(SocketAsyncEventArgs evt)
        {
            if (evt.UserToken is CommToken t)
            {
                (t.tactics as ExclusiveThreadedCommTactics).EnqueueEvent(evt);
            }
        }

        public ICommTactics RegisterChannel(ICommChannel channel)
        {
            return new ExclusiveThreadedCommTactics(channel, "Channel-"+(++counter));  
        }
    }
}
