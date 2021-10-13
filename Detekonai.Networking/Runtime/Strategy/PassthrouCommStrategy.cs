using Detekonai.Networking.Runtime.AsyncEvent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Detekonai.Networking.Runtime.Strategy
{
    public class PassthrouCommStrategy : IAsyncEventCommStrategy
    {

        public void EnqueueEvent(SocketAsyncEventArgs e)
        {
            if (e.UserToken is CommToken comm)
            {
                comm.callback(comm.ownerChannel, comm.blob, e);
            }
        }

        public ICommTactics RegisterChannel(ICommChannel channel)
        {
            return new PasshtrouCommTactics(channel);
        }
    }
}
