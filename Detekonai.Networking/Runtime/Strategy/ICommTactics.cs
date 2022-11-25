using Detekonai.Core;
using Detekonai.Networking.Runtime.AsyncEvent;
using Detekonai.Networking.Runtime.Raw;
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
		public delegate void RequestReceivedHandler(ICommChannel channel, BinaryBlob request, IRequestTicket ticket);

		public ICommChannel Owner { get; }
		public void EnqueueResponse(ushort responseIdx, BinaryBlob blob);
		public void ReleaseResponse(ushort responseIdx);
		public void CancelAllRequests();
		public IUniversalAwaiter<ICommResponse> CreateResponseAwaiter(ushort messageIdx);
		public IUniversalAwaiter<bool> CreateOpenAwaiter();


		RequestReceivedHandler RequestHandler { get; set; }
		IRawCommInterpreter RawDataInterpreter { get; set; }
		CommTacticsFinalizerHelper TacticsFinalizer { get;}
		//if this async make sure you copy the blob or you will have problems
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
