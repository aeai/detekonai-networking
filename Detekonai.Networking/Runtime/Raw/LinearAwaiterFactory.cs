using Detekonai.Networking.Runtime.AsyncEvent;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
                if(!resultCached)//we cache so we can call GetResult multiple times if needs be
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
        public IUniversalAwaiter<T> Create()
        {
            if (continuation != null)
            {
                Cancel();
            }
            status = AwaitResponseStatus.Pending;
            return new LinearAwaiter<T>(this);
        }

        public void Cancel()
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
