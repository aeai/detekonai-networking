using Detekonai.Core.Common.Runtime.ThreadAgent;
using Detekonai.Networking.Runtime.AsyncEvent;
using System;
using System.Collections.Concurrent;

namespace Detekonai.Networking.Runtime.Raw
{
    public class LinearAwaiterFactory<T>
    {

        private class LinearAwaiter<M> : IUniversalAwaiter<M>
        {
            private readonly LinearAwaiterFactory<M> owner;

            public bool IsCompleted { get; set; }

            public bool IsInitialized { get; set; }

            private M result;
            private bool resultCached = false;
            public void Cancel()
            {
                owner.Cancel();
            }

            public M GetResult()
            {
                if (!resultCached)//we cache so we can call GetResult multiple times if needs be
                {
                    resultCached = true;
                    owner.results.TryDequeue(out result);
                }
                return result;
            }

            public void OnCompleted(Action continuation)
            {
                owner.continuation = continuation;
                IsInitialized = true;
            }

            public LinearAwaiter(LinearAwaiterFactory<M> owner)
            {
                this.owner = owner;
            }
        }

        private Action continuation = null;
        private AwaitResponseStatus status = AwaitResponseStatus.Pending;
        private readonly ConcurrentQueue<T> results = new ConcurrentQueue<T>();
        private readonly IThreadAgent threadAgent;

        public LinearAwaiterFactory(IThreadAgent threadAgent)
        {
            this.threadAgent = threadAgent;
        }

        public IUniversalAwaiter<T> Create()
        {
            if (continuation != null)
            {
                throw new InvalidOperationException("Linear awaiter is currently pending, you need to cancel or finish before starting a new await!");
            }
            status = AwaitResponseStatus.Pending;
            return new LinearAwaiter<T>(this);
        }


        public void Cancel()
        {
            //TODO maybe we need a lock here? can this be called multiple times?
            threadAgent.ExecuteOnThread(CancelInternal);
        }

        public void CancelInternal()
        {
            if (status == AwaitResponseStatus.Pending)
            {
                status = AwaitResponseStatus.Canceled;
                Action cont = continuation;
                continuation = null;
                cont?.Invoke();
            }
        }

        public void SetResponse(T value)
        {
            if (status == AwaitResponseStatus.Pending)
            {
                results.Enqueue(value);
            }
        }

        public void Continue()
        {
            if (status == AwaitResponseStatus.Pending)
            {
                status = AwaitResponseStatus.Finished;
                Action cont = continuation;
                continuation = null;
                cont?.Invoke();
            }
        }
    }
}
