using Detekonai.Core;
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

		public ICommChannel owner;
		public HeaderFlags headerFlags;
		public ushort index;
		public int msgSize;
		public IAsyncEventHandlingStrategy strategy;
		public Action<ICommChannel, BinaryBlob, SocketAsyncEventArgs> callback;
		public BinaryBlob blob;

	}
}
