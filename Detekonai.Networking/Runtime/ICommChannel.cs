using Detekonai.Core;
using Detekonai.Core.Common;
using Detekonai.Networking.Runtime.AsyncEvent;
using Detekonai.Networking.Runtime.Raw;
using Detekonai.Networking.Runtime.Strategy;
using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Detekonai.Networking
{

	public interface ICommChannel : IDisposable
	{
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
		BinaryBlob CreateMessage(int poolIndex = 0, bool raw = false);
		BinaryBlob CreateMessageWithSize(int size = 0, bool raw = false);
		void Send(BinaryBlob blob);
		ICommTactics Tactics { get; }
		UniversalAwaitable<ICommResponse> SendRPC(BinaryBlob blob);
		UniversalAwaitable<ICommResponse> SendRPC(BinaryBlob blob, CancellationToken cancelationToken);

		public ILogConnector Logger {get; set;}
		EChannelStatus Status { get; }
		string Name { get; set; }
		bool Reliable { get; }
		EChannelMode Mode { get; set; }
	}
}
