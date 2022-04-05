using Detekonai.Core;
using Detekonai.Networking.Runtime.Strategy;
using System;
using System.Collections.Concurrent;
using System.Net.Sockets;

namespace Detekonai.Networking.Runtime.AsyncEvent
{
	public class SocketAsyncEventArgsPool
	{
		private ConcurrentBag<SocketAsyncEventArgs> pool = new ConcurrentBag<SocketAsyncEventArgs>();

		public SocketAsyncEventArgs Take(ICommChannel owner, IAsyncEventCommStrategy eventHandlingStrategy, ICommTactics tactics, Action<ICommChannel, BinaryBlob, SocketAsyncEventArgs> callback)
		{
			pool.TryTake(out SocketAsyncEventArgs args);
			if(args == null)
			{
				args = new SocketAsyncEventArgs
				{
					UserToken = new CommToken(),
				};
				args.Completed += new System.EventHandler<SocketAsyncEventArgs>(CompletedCallback);

			}
			var t = (CommToken)args.UserToken;
			t.ownerChannel = owner;
			t.blob = null;
			t.callback = callback;
			t.strategy = eventHandlingStrategy;
			t.tactics = tactics;
			return args;
		}

		private void CompletedCallback(object sender, SocketAsyncEventArgs e)
		{
			if (e.UserToken is CommToken comm)
            {
				if (comm.strategy != null)
				{
					comm.strategy.EnqueueEvent(e);
				}
            }
		}

		public void ConfigureSocketToWrite(BinaryBlob blob, SocketAsyncEventArgs evt)
		{
			evt.SetBuffer(blob.Owner.GetMemory(), blob.BufferAddress + blob.Index, blob.BytesWritten - blob.Index);
			(evt.UserToken as CommToken).blob = blob;
		}

		public void ConfigureSocketToRead(BinaryBlob blob, SocketAsyncEventArgs evt, int size = -1)
		{
			evt.SetBuffer(blob.Owner.GetMemory(), blob.BufferAddress + blob.Index, size == -1 ? blob.BufferSize - blob.Index : size);
			(evt.UserToken as CommToken).blob = blob;
		}

		public void Release(SocketAsyncEventArgs args)
		{
			var t = (CommToken)args.UserToken;
			t.blob?.Release();
			t.ownerChannel = null;
			t.ownerSocket = null;
			t.msgSize = 0;
			t.headerFlags = CommToken.HeaderFlags.None;
			t.index = 0;
			t.blob = null;
			t.strategy = null;
			t.tactics = null;
			args.AcceptSocket = null;
			args.SetBuffer(null, 0, 0);
			args.SocketFlags = SocketFlags.None;
			pool.Add(args);
		}
	}
}
