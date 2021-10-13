using Detekonai.Core;
using System;
using System.Net.Sockets;

namespace Detekonai.Networking.Runtime.Strategy
{
    public interface IAsyncEventCommStrategy : ICommStrategy
	{
		public void EnqueueEvent(SocketAsyncEventArgs evt);
	}
}
