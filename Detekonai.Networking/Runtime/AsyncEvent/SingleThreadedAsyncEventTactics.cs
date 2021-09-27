using Detekonai.Core;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Detekonai.Networking.Runtime.AsyncEvent
{
    public class SingleThreadedAsyncEventTactics : IAsyncEventHandlingTactics
    {
        private readonly ConcurrentQueue<ushort> cancelRequestQueue = new ConcurrentQueue<ushort>();
        private readonly ConcurrentDictionary<ushort, CommResponse> awaiters = new ConcurrentDictionary<ushort, CommResponse>();

        public ICommChannel Owner { get; private set; }


        private class OpenChannelResult
        {
            public AwaitResponseStatus requestStatus;
            public Action continuation;
        }

        private OpenChannelResult runningOpenRequest = null;

        public SingleThreadedAsyncEventTactics(ICommChannel channel)
        {
            Owner = channel;
        }

        struct SingleThreadedBlobAwaiter : IUniversalAwaiter<ICommResponse>
        {
            private readonly SingleThreadedAsyncEventTactics owner;
            private readonly ushort idx;

            public SingleThreadedBlobAwaiter(SingleThreadedAsyncEventTactics owner, ushort idx)
            {
                this.owner = owner;
                this.idx = idx;
                IsInitialized = false;
            }

            public bool IsCompleted
            {
                get
                {
                    return owner.awaiters.TryGetValue(idx, out CommResponse cwr) && cwr.Status != AwaitResponseStatus.Pending;
                }
            }

            public bool IsInitialized { get; private set; }

            public void Cancel()
            {
                owner.cancelRequestQueue.Enqueue(idx);
            }

            public ICommResponse GetResult()
            {
                owner.awaiters.TryGetValue(idx, out CommResponse val);
                return val;
            }

            public void OnCompleted(Action cont)
            {
                owner.awaiters[idx] = new CommResponse(idx, owner) { Continuation = cont };
                IsInitialized = true;
            }
        }

        struct ChannelOpenAwaiter : IUniversalAwaiter<bool>
        {
            private readonly SingleThreadedAsyncEventTactics owner;

            public bool IsCompleted
            {
                get
                {
                    return owner.Owner.Status == ICommChannel.EChannelStatus.Open || (owner.runningOpenRequest != null && owner.runningOpenRequest.requestStatus != AwaitResponseStatus.Pending);
                }
            }

            public bool IsInitialized { get; private set; }

            public ChannelOpenAwaiter(SingleThreadedAsyncEventTactics owner)
            {
                this.owner = owner;
                IsInitialized = false;
            }

            public void Cancel()
            {
                owner.runningOpenRequest.requestStatus = AwaitResponseStatus.Canceled;             
            }

            public bool GetResult()
            {
                return owner.Owner.Status == ICommChannel.EChannelStatus.Open;
            }

            public void OnCompleted(Action continuation)
            {
                owner.runningOpenRequest = new OpenChannelResult() { continuation = continuation, requestStatus = AwaitResponseStatus.Pending }; 
                IsInitialized = true;
            }
        }

        public void SignalOpenChannel()
        {
            if(runningOpenRequest != null)
            {
                runningOpenRequest.requestStatus = AwaitResponseStatus.Finished;
            }
        }

        public IUniversalAwaiter<bool> CreateOpenAwaiter()
        {

            return new ChannelOpenAwaiter(this);
        }

        public IUniversalAwaiter<ICommResponse> CreateResponseAwaiter(ushort messageIdx)
        {
            return new SingleThreadedBlobAwaiter(this, messageIdx);
        }

        public void ReleaseResponse(ushort responseIdx)
        {
            awaiters.TryRemove(responseIdx, out CommResponse r);
        }

        public void EnqueueResponse(ushort responseIdx, BinaryBlob blob)
        {
            if (awaiters.TryGetValue(responseIdx, out CommResponse cwr))
            {
                cwr.Blob = blob;
                cwr.Status = AwaitResponseStatus.Finished;
                cwr.Continuation?.Invoke();
            }
        }

        public void CancelAllRequests()
        {
            foreach (ushort cr in awaiters.Keys)
            {
                cancelRequestQueue.Enqueue(cr);
            }
            if (runningOpenRequest != null)
            {
                runningOpenRequest.requestStatus = AwaitResponseStatus.Canceled;
            }
        }


        public void Process()
        {
            while (!cancelRequestQueue.IsEmpty)
            {
                if (cancelRequestQueue.TryDequeue(out ushort idx))
                {
                    if (awaiters.TryGetValue(idx, out CommResponse cwr))
                    {
                        cwr.Status = AwaitResponseStatus.Canceled;
                        cwr.Continuation?.Invoke();
                    }
                }
            }

            if(runningOpenRequest != null && runningOpenRequest.requestStatus != AwaitResponseStatus.Pending)
            {
                if(runningOpenRequest.requestStatus == AwaitResponseStatus.Canceled)
                {
                    Owner.CloseChannel();
                }
                runningOpenRequest.continuation?.Invoke();
                runningOpenRequest = null;
            }
        }
    }
}
