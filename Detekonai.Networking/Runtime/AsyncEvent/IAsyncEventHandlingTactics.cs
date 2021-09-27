using Detekonai.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Detekonai.Networking.Runtime.AsyncEvent
{
    public interface IAsyncEventHandlingTactics
    {
		public ICommChannel Owner { get; }
		public void EnqueueResponse(ushort responseIdx, BinaryBlob blob);
		public void ReleaseResponse(ushort responseIdx);
		public void CancelAllRequests();
		public IUniversalAwaiter<ICommResponse> CreateResponseAwaiter(ushort messageIdx);
		public void SignalOpenChannel();
		public IUniversalAwaiter<bool> CreateOpenAwaiter();
	}
}
