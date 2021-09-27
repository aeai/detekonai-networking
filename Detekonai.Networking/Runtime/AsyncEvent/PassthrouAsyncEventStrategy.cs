using Detekonai.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Detekonai.Networking.Runtime.AsyncEvent
{
    public class PassthrouAsyncEventStrategy : IAsyncEventHandlingStrategy
    {

        public void EnqueueEvent(SocketAsyncEventArgs evt)
        {
            if (evt.UserToken is CommToken comm)
            {
                comm.callback(comm.owner, comm.blob, evt);
            }
        }

        public IAsyncEventHandlingTactics RegisterChannel(ICommChannel channel)
        {
            throw new NotImplementedException();
        }


        public void UnregisterChannel(IAsyncEventHandlingTactics channelTactics)
        {
            throw new NotImplementedException();
        }
    }
}
