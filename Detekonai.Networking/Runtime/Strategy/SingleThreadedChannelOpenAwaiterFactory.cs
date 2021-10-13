using Detekonai.Networking.Runtime.AsyncEvent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Detekonai.Networking.Runtime.Strategy
{
    public class SingleThreadedChannelOpenAwaiterFactory
    {
        private readonly ICommChannel owner;
        
        public event Action OnFinished;

        private class OpenChannelResult
        {
            public AwaitResponseStatus requestStatus;
            public Action continuation;
        }

        private OpenChannelResult runningOpenRequest  = null;

        private struct ChannelOpenAwaiter : IUniversalAwaiter<bool>
        {
            private readonly SingleThreadedChannelOpenAwaiterFactory owner;

            public bool IsCompleted
            {
                get
                {
                    return owner.owner.Status == ICommChannel.EChannelStatus.Open || (owner.runningOpenRequest != null && owner.runningOpenRequest.requestStatus != AwaitResponseStatus.Pending);
                }
            }

            public bool IsInitialized { get; private set; }

            public ChannelOpenAwaiter(SingleThreadedChannelOpenAwaiterFactory owner)
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
                return owner.owner.Status == ICommChannel.EChannelStatus.Open;
            }

            public void OnCompleted(Action continuation)
            {
                owner.runningOpenRequest = new OpenChannelResult() { continuation = continuation, requestStatus = AwaitResponseStatus.Pending };
                IsInitialized = true;
            }
        }


        public bool Finished {
            get
            {
                return runningOpenRequest != null && runningOpenRequest.requestStatus != AwaitResponseStatus.Pending;
            }
        }

        public bool Canceled
        {
            get
            {
                return runningOpenRequest != null && runningOpenRequest.requestStatus == AwaitResponseStatus.Canceled;
            }
        }

        public SingleThreadedChannelOpenAwaiterFactory(ICommChannel channel)
        {
            owner = channel;
        }

        public void SignalOpenChannel()
        {
            if (runningOpenRequest != null)
            {
                runningOpenRequest.requestStatus = AwaitResponseStatus.Finished;
                OnFinished?.Invoke();
            }
        }

        public IUniversalAwaiter<bool> Create()
        {
            return new ChannelOpenAwaiter(this);
        }

        public void Invoke()
        {
            if (Finished)
            {
                if (Canceled)
                {
                    owner.CloseChannel();
                }
                runningOpenRequest.continuation?.Invoke();
                runningOpenRequest = null;
            }
        }

        public void Cancel()
        {
            if (runningOpenRequest != null)
            {
                runningOpenRequest.requestStatus = AwaitResponseStatus.Canceled;
                OnFinished?.Invoke();
            }
        }



    }
}
