using Detekonai.Core;
using System;
using System.Net.Sockets;

namespace Detekonai.Networking.Runtime.AsyncEvent
{
    public interface IAsyncEventHandlingStrategy
	{
		public void EnqueueEvent(SocketAsyncEventArgs evt);
		IAsyncEventHandlingTactics RegisterChannel(ICommChannel channel);
		void UnregisterChannel(IAsyncEventHandlingTactics channelTactics);
	}
}
