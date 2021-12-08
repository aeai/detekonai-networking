﻿using Detekonai.Networking.Runtime.AsyncEvent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Detekonai.Networking.Runtime.Raw
{
    public class SingletonAwaiterFactory<T>
    {

        private class SingletonAwaiter<M> : IUniversalAwaiter<M>
        {
            private readonly SingletonAwaiterFactory<M> owner;

            public bool IsCompleted { get; set; }

            public bool IsInitialized { get; set; }

            public void Cancel()
            {
                owner.Cancel();
            }

            public M GetResult()
            {
                return owner.result;
            }

            public void OnCompleted(Action continuation)
            {
                owner.continuation = continuation;
                IsInitialized = true;
            }

            public SingletonAwaiter(SingletonAwaiterFactory<M> owner)
            {
                this.owner = owner;
            }
        }

        private Action continuation = null;
        private T result = default;
        private AwaitResponseStatus status = AwaitResponseStatus.Pending;

        public IUniversalAwaiter<T> Create()
        {
            if (continuation != null)
            {
                Cancel();
            }
            result = default;
            status = AwaitResponseStatus.Pending;
            return new SingletonAwaiter<T>(this);
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
                result = value;
                status = AwaitResponseStatus.Finished;
                Action cont = continuation;
                continuation = null;
                cont?.Invoke();
            }
        }
    }
}
