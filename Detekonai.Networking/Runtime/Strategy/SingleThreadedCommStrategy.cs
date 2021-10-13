using Detekonai.Core;
using Detekonai.Networking.Runtime.AsyncEvent;
using System;
using System.Collections.Concurrent;
using System.Net.Sockets;

namespace Detekonai.Networking.Runtime.Strategy
{
    public class SingleThreadedCommStrategy : IAsyncEventCommStrategy
	{
		private readonly ConcurrentQueue<SocketAsyncEventArgs> callbackQueue = new ConcurrentQueue<SocketAsyncEventArgs>();
        private Action OnProcess;

        public ICommTactics RegisterChannel(ICommChannel channel)
        {
            SingleThreadedCommTactics tac = new SingleThreadedCommTactics(channel);
            OnProcess += tac.Process;
            tac.OnTacticsCompleted += UnregisterChannel;
            return tac;
        }

        public void UnregisterChannel(ICommTactics channelTactics)
        {
            channelTactics.OnTacticsCompleted -= UnregisterChannel;
            OnProcess -= (channelTactics as SingleThreadedCommTactics).Process;
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
						comm.callback(comm.ownerChannel, comm.blob, e);
					}
				}
			}

            OnProcess?.Invoke();
        }
    }
}
