using Detekonai.Core;
using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using static Detekonai.Networking.Runtime.AsyncEvent.IAsyncEventHandlingStrategy;

namespace Detekonai.Networking.Runtime.AsyncEvent
{
    public class SingleThreadedAsyncEventStrategy : IAsyncEventHandlingStrategy
	{
		private readonly ConcurrentQueue<SocketAsyncEventArgs> callbackQueue = new ConcurrentQueue<SocketAsyncEventArgs>();
        private Action OnProcess;

        public IAsyncEventHandlingTactics RegisterChannel(ICommChannel channel)
        {
            SingleThreadedAsyncEventTactics tac = new SingleThreadedAsyncEventTactics(channel);
            OnProcess += tac.Process;
            return tac;
        }

        public void UnregisterChannel(IAsyncEventHandlingTactics channelTactics)
        {
            
            OnProcess -= (channelTactics as SingleThreadedAsyncEventTactics).Process;
        }

        public void EnqueueEvent(SocketAsyncEventArgs evt)
		{
			callbackQueue.Enqueue(evt);
		}

        public void Process()
		{
			while (!callbackQueue.IsEmpty)
			{
				if (callbackQueue.TryDequeue(out SocketAsyncEventArgs e))
				{
					if (e.UserToken is CommToken comm)
					{
						comm.callback(comm.owner, comm.blob, e);
					}
				}
			}


            OnProcess?.Invoke();
        }
    }
}
