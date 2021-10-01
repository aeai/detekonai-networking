using Detekonai.Core;
using Detekonai.Core.Common;
using Detekonai.Networking.Runtime.AsyncEvent;
using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Detekonai.Networking
{

	public interface ICommChannel : ILogCapable, IDisposable
	{
		public delegate void BlobReceivedHandler(ICommChannel channel, BinaryBlob e);
		public delegate int RawDataReceiveHandler(ICommChannel channel, BinaryBlob e, int bytesTransferred);
		public delegate void CommChannelChangeHandler(ICommChannel channel);
		public delegate BinaryBlob RequestReceivedHandler(ICommChannel channel, BinaryBlob request);
		public enum EChannelStatus
		{
			Open,
			Establishing,
			Closed,
		}

		public enum EChannelMode {
			Managed,
			Raw,
		}

		void CloseChannel();
		UniversalAwaitable<bool> OpenChannel();
		UniversalAwaitable<bool> OpenChannel(CancellationToken cancelationToken);
		BinaryBlob CreateMessage(bool raw = false);
		
		void Send(BinaryBlob blob);
		UniversalAwaitable<ICommResponse> SendRPC(BinaryBlob blob);
		UniversalAwaitable<ICommResponse> SendRPC(BinaryBlob blob, CancellationToken cancelationToken);

		// these will block so be fast
		event BlobReceivedHandler OnBlobReceived;
		RequestReceivedHandler RequestHandler { get; set; }
		RawDataReceiveHandler RawDataReceiver { get; set; }

		event CommChannelChangeHandler OnRequestSent;
		event CommChannelChangeHandler OnConnectionStatusChanged;
		EChannelStatus Status { get; }
		string Name { get; set; }
		bool Reliable { get; }
		EChannelMode Mode { get; set; }
	}
}
