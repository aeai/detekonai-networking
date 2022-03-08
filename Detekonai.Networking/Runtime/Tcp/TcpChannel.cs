using Detekonai.Core;
using Detekonai.Core.Common;
using Detekonai.Networking.Runtime.AsyncEvent;
using Detekonai.Networking.Runtime.Raw;
using Detekonai.Networking.Runtime.Strategy;
using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using static Detekonai.Core.Common.ILogConnector;
using static Detekonai.Networking.ICommChannel;
using static Detekonai.Networking.Runtime.Tcp.TcpPacketBuilder;

namespace Detekonai.Networking.Runtime.Tcp
{
	public sealed class TcpChannel : ICommChannel, ITcpPacketHandler
	{

		private enum SystemMessage
		{
			ChannelReady = 0,
			Ping = 1,
		}

		private const ushort headerSize = 6;

		private enum EReadingMode
		{
			Header,
			Data,
		}

		private class TcpChannelRequestTicket : IRequestTicket
		{
			private readonly TcpChannel channel;
			private readonly ushort msgIdx;

			public TcpChannelRequestTicket(TcpChannel channel, ushort msgIdx)
			{
				this.channel = channel;
				this.msgIdx = msgIdx;
			}

			public void Fulfill(BinaryBlob blob)
			{
				if (blob != null)
				{
					blob.AddUShort(msgIdx);
					channel.Send(blob, CommToken.HeaderFlags.RpcAck);
				}
			}
		}

		private Socket client;
		private SocketAsyncEventArgsPool eventPool;
		private BinaryBlobPool[] bufferPool;
		private ushort msgIndex = 1;
		public ILogConnector Logger { get; set; }

		private ICommChannel.EChannelStatus status = ICommChannel.EChannelStatus.Closed;
		private IAsyncEventCommStrategy eventHandlingStrategy;
		public ICommTactics Tactics { get; private set; }
		public IPEndPoint Endpoint { get; private set; }

		private int rawPoolIndex = 0;
		public int RawPoolIndex
		{
			get
			{
				return rawPoolIndex;
			}
			set
			{
				if (value < 0 || value >= bufferPool.Length)
				{
					throw new InvalidOperationException($"RawPool index {value} is outside of the [0; pool count({bufferPool.Length})] range!");
				}
				rawPoolIndex = value;
			}
		}
		public bool Reliable => true;
		public string Name { get; set; }

		private TcpPacketBuilder builder;
		public ICommChannel.EChannelMode Mode
		{
			get
			{
				return builder.Mode;
			}
			set
			{
				builder.Mode = value;
			}
		}

		public ICommChannel.EChannelStatus Status
		{
			get
			{
				return status;
			}
			private set
			{
				if (value != status)
				{
					status = value;
					Tactics.StatusChanged();
				}
			}
		}

		public int RawPoolSize => bufferPool[RawPoolIndex].BlobSize;

        public TcpChannel(IPEndPoint endpoint, IAsyncEventCommStrategy eventHandlingStrategy, SocketAsyncEventArgsPool eventPool, params BinaryBlobPool[] bufferPool)
		{
			this.Endpoint = endpoint;
			this.eventPool = eventPool;
			this.bufferPool = bufferPool;
			this.eventHandlingStrategy = eventHandlingStrategy;
			Tactics = eventHandlingStrategy.RegisterChannel(this);
			builder = new TcpPacketBuilder(this);
		}

		public TcpChannel(IAsyncEventCommStrategy eventHandlingStrategy, SocketAsyncEventArgsPool eventPool, params BinaryBlobPool[] bufferPool) : this(null, eventHandlingStrategy, eventPool, bufferPool)
		{
		}

		public TcpChannel(string ip, int port, IAsyncEventCommStrategy eventHandlingStrategy, SocketAsyncEventArgsPool eventPool, params BinaryBlobPool[] bufferPool) : this(new IPEndPoint(IPAddress.Parse(ip), port), eventHandlingStrategy, eventPool, bufferPool)
		{
		}

		public void AssignSocket(Socket socket)
		{
			if (client != null)
			{
				CloseChannel();
			}
			var rend = (IPEndPoint)socket.RemoteEndPoint;
			Endpoint = rend;
			client = socket;
			Status = ICommChannel.EChannelStatus.Open;
			Logger?.Log(this, "Channel opened with socket assignment", LogLevel.Info);

			ReceiveData(headerSize, null);
		}



		public void CloseChannel()
		{
			if (client != null)
			{
				Logger?.Log(this, "Channel closed", LogLevel.Verbose);
			}
			Tactics.CancelAllRequests();
			client?.Close();
			client?.Dispose();
			client = null;
			Status = ICommChannel.EChannelStatus.Closed;
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "SocketAsyncEventArgs handled by a pool, no dispose requred here")]
		public UniversalAwaitable<bool> OpenChannel()
		{
			if (Endpoint == null)
			{
				Logger?.Log(this, "The channel address is not set, can't open the channel!", LogLevel.Error);
				throw new InvalidOperationException("The channel address is not set, can't open the channel!");
			}
			msgIndex = 1;
			client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			Status = ICommChannel.EChannelStatus.Establishing;
			SocketAsyncEventArgs evt = eventPool.Take(this, eventHandlingStrategy, Tactics, HandleEvent);
			evt.RemoteEndPoint = Endpoint;
			IUniversalAwaiter<bool> res = Tactics.CreateOpenAwaiter();
			if (!client.ConnectAsync(evt))
			{
				eventHandlingStrategy.EnqueueEvent(evt);
			}
			return new UniversalAwaitable<bool>(res);
		}
		public UniversalAwaitable<bool> OpenChannel(CancellationToken cancelationToken)
		{
			UniversalAwaitable<bool> res = OpenChannel();
			cancelationToken.Register(res.CancelRequest);
			return res;
		}

		public UniversalAwaitable<ICommResponse> SendRPC(BinaryBlob blob, CancellationToken cancelationToken)
		{
			UniversalAwaitable<ICommResponse> res = SendRPC(blob);
			cancelationToken.Register(res.CancelRequest);
			return res;
		}

		public UniversalAwaitable<ICommResponse> SendRPC(BinaryBlob blob)
		{
			IUniversalAwaiter<ICommResponse> awaiter = Send(blob, CommToken.HeaderFlags.RequiresAnswer);
			if (awaiter == null)
			{
				throw new InvalidOperationException("Trying to send in a closed channel!");
			}
			return new UniversalAwaitable<ICommResponse>(awaiter);
		}

		public void CommReceived(CommToken token)
		{
			BinaryBlob blob = token.blob;

			if ((token.headerFlags & CommToken.HeaderFlags.RpcAck) == CommToken.HeaderFlags.RpcAck)
			{
				blob.Index = token.msgSize - 2;
				ushort ackIndex = blob.ReadUShort();
				blob.JumpIndexToBegin();
				token.blob = null;//don't release the blob we need to keep it around for TAP, we release later
				Tactics.EnqueueResponse(ackIndex, blob);
			}
			else if ((token.headerFlags & CommToken.HeaderFlags.RequiresAnswer) == CommToken.HeaderFlags.RequiresAnswer)
			{
				Tactics.RequestHandler?.Invoke(this, blob, new TcpChannelRequestTicket(this, token.index));
			}
			else if ((token.headerFlags & CommToken.HeaderFlags.SystemPackage) == CommToken.HeaderFlags.SystemPackage)
			{
				HandleProtocolBlob(blob);
			}
			else
			{
				//beware: if we go async we may release the blob before the async function finishes
				Tactics.BlobRecieved(blob);
			}
		}
		private bool HandleConnectEvent(SocketAsyncEventArgs e)
        {
			if (e.SocketError == SocketError.Success)
			{
				Logger?.Log(this, "Channel open", LogLevel.Info);
				if (Mode == ICommChannel.EChannelMode.Managed)
				{
					ReceiveData(headerSize, null);
				}
				else
				{
					ReceiveData(bufferPool[RawPoolIndex].BlobSize, null);
				}
				Status = ICommChannel.EChannelStatus.Open;
			}
			else
			{
				Status = ICommChannel.EChannelStatus.Closed;
				Logger?.Log(this, $"Channel closed becuase error: {e.SocketError}", LogLevel.Error);
			}
			return true;
		}
		private bool HandleReceiveEvent(CommToken token, SocketAsyncEventArgs e)
		{
			//TODO this but better, and make it work maybe use bytesWritten?
			token.blob.Index += e.BytesTransferred;
			builder.Receive(e.UserToken as CommToken, e.BytesTransferred, e);
			return e.BytesTransferred == 0;
		}

		public void EndOfStream() 
		{
			CloseChannel();
			Logger?.Log(this, $"Closing channel becuase recevied EOS", LogLevel.Info);
		}

		private bool HandleSendEvent(SocketAsyncEventArgs e)
        {
			if (e.SocketError != SocketError.Success)
			{
				CloseChannel();
				Logger?.Log(this, $"Closing channel becuase error: {e.SocketError}", LogLevel.Error);
			}
			else
			{
				Logger?.Log(this, $"Sent: {e.BytesTransferred}", LogLevel.Info);
				Tactics.RequestSent();
			}
			return true;
		}

		public void HandleEvent(ICommChannel channel, BinaryBlob blob, SocketAsyncEventArgs e)
		{
			bool releaseEvent = false;
			if (e.LastOperation == SocketAsyncOperation.Connect)
			{
				releaseEvent = HandleConnectEvent(e);
			}
			else if (e.LastOperation == SocketAsyncOperation.Receive)
			{
				releaseEvent = HandleReceiveEvent(e.UserToken as CommToken, e);
			}
            else if (e.LastOperation == SocketAsyncOperation.Send)
			{
				releaseEvent = HandleSendEvent(e);
			}

			if (releaseEvent)
			{
				eventPool.Release(e);
			}
        }

		private void HandleProtocolBlob(BinaryBlob blob)
		{
			SystemMessage type = (SystemMessage)blob.ReadByte();
			if (type == SystemMessage.Ping)
			{
				//TODO
			}
		}

		public void Send(BinaryBlob blob)
		{
			Send(blob, CommToken.HeaderFlags.None);
		}
		void Send(BinaryBlob blob, IRawCommInterpreter interpreter)
		{
			Send(blob, CommToken.HeaderFlags.None, interpreter);
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "SocketAsyncEventArgs handled by a pool, no dispose requred here")]
		private IUniversalAwaiter<ICommResponse> Send(BinaryBlob blob, CommToken.HeaderFlags flags, IRawCommInterpreter interpreter = null)
		{
			ushort sentIndex = 0;
			IUniversalAwaiter<ICommResponse> returnVal = null;
			if (msgIndex == 0) //msg index overflow
			{
				msgIndex = 1;
			}
			if (Status == ICommChannel.EChannelStatus.Open)
			{
				bool headless = blob.RemoveBufferPrefix() == 0;
				blob.JumpIndexToBegin();
				sentIndex = msgIndex++;
				if (!headless)
				{
					AddHeader(blob, flags, (uint)(blob.BytesWritten - headerSize), sentIndex);
					blob.JumpIndexToBegin();
				}
				SocketAsyncEventArgs evt = eventPool.Take(this, eventHandlingStrategy, Tactics, HandleEvent);
				eventPool.ConfigureSocketToWrite(blob, evt);
				//we need this after we have the sent index but before the actuall sending or we may end up having the answer before we have the TCS
				if ((flags & CommToken.HeaderFlags.RequiresAnswer) == CommToken.HeaderFlags.RequiresAnswer)
				{
					returnVal = Tactics.CreateResponseAwaiter(sentIndex);
				}
				if (!client.SendAsync(evt))
				{
					eventHandlingStrategy.EnqueueEvent(evt);
				}
			}
			else
			{
				Logger?.Log(this, "Trying to send on a closed channel!", LogLevel.Error);
			}
			return returnVal;
		}

		private void Ping()
		{
			BinaryBlob blob = CreateMessage();
			blob.AddByte((byte)SystemMessage.Ping);
			Send(blob, CommToken.HeaderFlags.SystemPackage);
		}


		public BinaryBlob GetBlobFromPool(int size)
		{
			int poolIdx = -1;
			for (int i = 0; i < bufferPool.Length; i++)
			{
				if (size <= bufferPool[i].BlobSize)
				{
					poolIdx = i;
					break;
				}
			}
			if (poolIdx == -1)
			{
				throw new InvalidOperationException($"Try to read a package which is bigger then the max buffer size! {size}> {bufferPool[bufferPool.Length - 1].BlobSize}");
			}
			return bufferPool[poolIdx].GetBlob();
		}

		[System.Diagnostics.CodeAnalysis.SuppressMessage("Reliability", "CA2000:Dispose objects before losing scope", Justification = "SocketAsyncEventArgs handled by a pool, no dispose requred here")]
		public void ReceiveData(int size, BinaryBlob originalBlob = null, int partialDataSize = 0)
		{
			var evt = eventPool.Take(this, eventHandlingStrategy, Tactics, HandleEvent);

			BinaryBlob blob = GetBlobFromPool(size);

			if (originalBlob != null)
			{
				blob.CopyDataFrom(originalBlob, partialDataSize);
			}

			ContinueReceivingData(size, blob, evt);
		}
		public void ContinueReceivingData(int size, BinaryBlob blob, SocketAsyncEventArgs evt)
		{
			eventPool.ConfigureSocketToRead(blob, evt, size);
			if (client != null && !client.ReceiveAsync(evt))
			{
				eventHandlingStrategy.EnqueueEvent(evt);
			}
		}

		public BinaryBlob CreateMessageWithSize(int size = 0, bool raw = false)
		{
			int poolIdx = -1;
			for (int i = 0; i < bufferPool.Length; i++)
			{
				if (size < bufferPool[i].BlobSize)
				{
					poolIdx = i;
					break;
				}
			}
			if (poolIdx == -1)
			{
				throw new InvalidOperationException($"Try to create message which is bigger then the max buffer size! {size}> {bufferPool[bufferPool.Length - 1].BlobSize}");
			}
			return CreateMessage(poolIdx, raw);
		}

		public BinaryBlob CreateMessage(int poolIndex = 0, bool raw = false)
		{
			BinaryBlob blob = bufferPool[poolIndex].GetBlob();
			if (!raw)
			{
				blob.PrefixBuffer(headerSize);
			}
			return blob;
		}

		public void Dispose()
		{
			CloseChannel();
			Tactics.Shutdown();
		}

    }
}
