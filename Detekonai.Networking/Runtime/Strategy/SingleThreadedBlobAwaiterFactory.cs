using Detekonai.Core;
using Detekonai.Networking.Runtime.AsyncEvent;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Detekonai.Networking.Runtime.Strategy
{
    public class SingleThreadedBlobAwaiterFactory
    {
        private readonly ConcurrentDictionary<ushort, CommResponse> awaiters = new ConcurrentDictionary<ushort, CommResponse>();

        private readonly ICommTactics owner;

        private readonly Action<ushort> cancelAction;

        private struct SingleThreadedBlobAwaiter : IUniversalAwaiter<ICommResponse>
        {
            private readonly SingleThreadedBlobAwaiterFactory owner;
            private readonly ushort idx;

            public SingleThreadedBlobAwaiter(SingleThreadedBlobAwaiterFactory owner, ushort idx)
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
                owner.cancelAction?.Invoke(idx);
            }

            public ICommResponse GetResult()
            {
                owner.awaiters.TryGetValue(idx, out CommResponse val);
                return val;
            }

            public void OnCompleted(Action cont)
            {
                owner.awaiters[idx] = new CommResponse(idx, owner.owner) { Continuation = cont };
                IsInitialized = true;
            }
        }

        public SingleThreadedBlobAwaiterFactory(ICommTactics owner, Action<ushort> cancelAction)
        {
            this.owner = owner;
            this.cancelAction = cancelAction;
        }

        public IUniversalAwaiter<ICommResponse>  Create(ushort messageIdx)
        {
            return new SingleThreadedBlobAwaiter(this, messageIdx);
        }

        public void ScheduleCancelAll()
        {
            foreach (ushort cr in awaiters.Keys)
            {
                cancelAction(cr);
            }
        }

        public void Cancel(ushort msgIdx)
        {
            if (awaiters.TryGetValue(msgIdx, out CommResponse cwr))
            {
                if (cwr.Status == AwaitResponseStatus.Pending)
                {
                    cwr.Status = AwaitResponseStatus.Canceled;
                    cwr.Continuation?.Invoke();
                }
            }
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
    }
}
