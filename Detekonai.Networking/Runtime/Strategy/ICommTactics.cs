using Detekonai.Core;
using Detekonai.Networking.Runtime.AsyncEvent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Detekonai.Networking.Runtime.Strategy
{
    public interface ICommTactics
    {
		public delegate void BlobReceivedHandler(ICommChannel channel, BinaryBlob e);
		public delegate void TacticsCompleted(ICommTactics tactics);
		public delegate void CommChannelChangeHandler(ICommChannel channel);
		public delegate int RawDataReceiveHandler(ICommChannel channel, BinaryBlob e, int bytesTransferred);
		public delegate BinaryBlob RequestReceivedHandler(ICommChannel channel, BinaryBlob request);

		public ICommChannel Owner { get; }
		public void EnqueueResponse(ushort responseIdx, BinaryBlob blob);
		public void ReleaseResponse(ushort responseIdx);
		public void CancelAllRequests();
		public IUniversalAwaiter<ICommResponse> CreateResponseAwaiter(ushort messageIdx);
		public IUniversalAwaiter<bool> CreateOpenAwaiter();


		RequestReceivedHandler RequestHandler { get; set; }
		RawDataReceiveHandler RawDataReceiver { get; set; }
		CommTacticsFinalizerHelper TacticsFinalizer { get;}
		event BlobReceivedHandler OnBlobReceived;
		event CommChannelChangeHandler OnRequestSent;
		event CommChannelChangeHandler OnConnectionStatusChanged;
		event TacticsCompleted OnTacticsCompleted;

		public void BlobRecieved(BinaryBlob e);
		public void RequestSent();
		public void StatusChanged();
		public void Shutdown();
	}
}
