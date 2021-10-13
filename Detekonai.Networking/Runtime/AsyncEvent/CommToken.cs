using Detekonai.Core;
using Detekonai.Networking.Runtime.Strategy;
using System;
using System.Net.Sockets;

namespace Detekonai.Networking.Runtime.AsyncEvent
{
    public class CommToken
	{
		[Flags]
		public enum HeaderFlags
		{
			None = 0,
			LargePackage = 1,
			SystemPackage = 2,
			RequiresAnswer = 4,
			RpcAck = 8
		}

		public ICommChannel ownerChannel;
		public Socket ownerSocket;
		public HeaderFlags headerFlags;
		public ushort index;
		public int msgSize;
		public IAsyncEventCommStrategy strategy;
		public ICommTactics tactics;
		public Action<ICommChannel, BinaryBlob, SocketAsyncEventArgs> callback;
		public BinaryBlob blob;

	}
}
