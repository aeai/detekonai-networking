using Detekonai.Core.Common.Runtime.ThreadAgent;
using Detekonai.Networking.Runtime.AsyncEvent;
using System;

namespace Detekonai.Networking.Runtime.Strategy
{
    public class SingleThreadedChannelOpenAwaiterFactory
    {
        private readonly ICommChannel owner;
        private readonly IThreadAgent threadAgent;

        //public event Action OnFinished;

        private class OpenChannelResult
        {
            public AwaitResponseStatus requestStatus;
            public Action continuation;
        }

        private OpenChannelResult runningOpenRequest = null;

        private struct ChannelOpenAwaiter : IUniversalAwaiter<bool>
        {
            private readonly SingleThreadedChannelOpenAwaiterFactory owner;

            public bool IsCompleted => owner.owner.Status == ICommChannel.EChannelStatus.Open || (owner.runningOpenRequest != null && owner.runningOpenRequest.requestStatus != AwaitResponseStatus.Pending);

            public bool IsInitialized => owner.runningOpenRequest != null;

            public ChannelOpenAwaiter(SingleThreadedChannelOpenAwaiterFactory owner)
            {
                this.owner = owner;
            }

            public void Cancel()
            {
                owner.threadAgent.ExecuteOnThread(CancelSafe);
            }
            private void CancelSafe()
            {
                if (IsInitialized == false)
                {
                    owner.runningOpenRequest = new OpenChannelResult();
                }
                owner.runningOpenRequest.requestStatus = AwaitResponseStatus.Canceled;
                owner.Finish();
            }

            public bool GetResult()
            {
                return owner.owner.Status == ICommChannel.EChannelStatus.Open;
            }

            public void OnCompleted(Action continuation)
            {
                if (IsInitialized)
                {
                    owner.runningOpenRequest.continuation = continuation;
                }
                else
                {
                    owner.runningOpenRequest = new OpenChannelResult() { continuation = continuation, requestStatus = AwaitResponseStatus.Pending };
                }
            }
        }


        public bool Finished => runningOpenRequest != null && runningOpenRequest.requestStatus != AwaitResponseStatus.Pending;

        public bool Canceled => runningOpenRequest != null && runningOpenRequest.requestStatus == AwaitResponseStatus.Canceled;

        public SingleThreadedChannelOpenAwaiterFactory(ICommChannel channel, IThreadAgent threadAgent)
        {
            owner = channel;
            this.threadAgent = threadAgent;
        }

        public void SignalOpenChannel()
        {
            if (runningOpenRequest != null)
            {
                runningOpenRequest.requestStatus = AwaitResponseStatus.Finished;
                threadAgent.ExecuteOnThread(Finish);
            }
        }

        public IUniversalAwaiter<bool> Create()
        {
            return new ChannelOpenAwaiter(this);
        }

        private void InternalCancel() 
        {
            if (runningOpenRequest != null)
            {
                runningOpenRequest = new OpenChannelResult();
            }
            runningOpenRequest.requestStatus = AwaitResponseStatus.Canceled;
            Finish();
        }

        private void Finish()
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
            threadAgent.ExecuteOnThread(() =>
            {
                if (runningOpenRequest != null)
                {
                    runningOpenRequest.requestStatus = AwaitResponseStatus.Canceled;
                    Finish();
                }
            });
        }



    }
}
