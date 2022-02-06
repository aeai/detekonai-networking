using Detekonai.Core;
using Detekonai.Networking.Runtime.AsyncEvent;
using Detekonai.Networking.Runtime.Raw;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Detekonai.Networking.Runtime.Strategy
{
    public class ExclusiveThreadedCommTactics : ICommTactics
    {

        private SingleThreadedChannelOpenAwaiterFactory openAwaiterFactory = null;
        private SingleThreadedBlobAwaiterFactory blobAwaiterFactory = null;
        public ICommChannel Owner { get; private set; }

        private ChannelSyncronizationContext ctx;

        public event ICommTactics.BlobReceivedHandler OnBlobReceived;
        public event ICommTactics.CommChannelChangeHandler OnRequestSent;
        public event ICommTactics.CommChannelChangeHandler OnConnectionStatusChanged;
        public event ICommTactics.TacticsCompleted OnTacticsCompleted;

        public bool Active { get; set; } = true;
        public ICommTactics.RequestReceivedHandler RequestHandler { get; set; }
        public IRawCommInterpreter RawDataInterpreter { get; set; }

        public CommTacticsFinalizerHelper TacticsFinalizer { get; private set; }

        public ExclusiveThreadedCommTactics(ICommChannel owner, string name)
        {
            Owner = owner;
            ctx = new ChannelSyncronizationContext(name+"Thread");
            openAwaiterFactory = new SingleThreadedChannelOpenAwaiterFactory(owner);
            openAwaiterFactory.OnFinished += OpenAwaiterFactory_OnFinished;
            blobAwaiterFactory = new SingleThreadedBlobAwaiterFactory(this, CancelRequest);
            TacticsFinalizer = new CommTacticsFinalizerHelper(() => {
                ctx.Close();
                OnTacticsCompleted?.Invoke(this);
            } );
        }

        public void Post(SendOrPostCallback callback, object ob)
        {
            ctx.Post(callback, ob);
        }

        private void OpenAwaiterFactory_OnFinished()
        {
            ctx.Post(FinishChannelOpen, null);
        }

        private void FinishChannelOpen(object ob)
        {
            openAwaiterFactory.Invoke();
        }

        public void Shutdown()
        {
            ctx.Post((object ob) => TacticsFinalizer.Execute(), null);
        }

        public void EnqueueEvent(SocketAsyncEventArgs evt)
        {
            if (evt.UserToken is CommToken t)
            {
                ctx.Post(RunCommCallback, evt);
            }
        }

        private void CancelRequestCallback(object idx)
        {
            blobAwaiterFactory.Cancel((ushort)idx);
        }

        private void CancelRequest(ushort idx)
        {
            ctx.Post(CancelRequestCallback, idx);
        }

        private void RunCommCallback(object e)
        {
            SocketAsyncEventArgs evt = (SocketAsyncEventArgs)e;
            if (evt.UserToken is CommToken t)
            {
               t.callback(t.ownerChannel, t.blob, evt);
            }
        }

        public void CancelAllRequests()
        {
            blobAwaiterFactory.ScheduleCancelAll();
            openAwaiterFactory.Cancel();
        }

        public IUniversalAwaiter<bool> CreateOpenAwaiter()
        {
            return openAwaiterFactory.Create();
        }

        public IUniversalAwaiter<ICommResponse> CreateResponseAwaiter(ushort messageIdx)
        {
            return blobAwaiterFactory.Create(messageIdx);
        }

        public void EnqueueResponse(ushort responseIdx, BinaryBlob blob)
        {
            blobAwaiterFactory.EnqueueResponse(responseIdx, blob);
        }

        public void ReleaseResponse(ushort responseIdx)
        {
            blobAwaiterFactory.ReleaseResponse(responseIdx);
        }

        public void SignalOpenChannel()
        {
            openAwaiterFactory.SignalOpenChannel();
        }

        public void BlobRecieved(BinaryBlob e)
        {
            int startIdx = e.Index;
            foreach (ICommTactics.BlobReceivedHandler del in OnBlobReceived.GetInvocationList())
            {
                e.Index = startIdx;
                del(Owner,e);
            }
        }

        public void RequestSent()
        {
            OnRequestSent?.Invoke(Owner);
        }

        public void StatusChanged()
        {
            if (Owner.Status == ICommChannel.EChannelStatus.Open)
            {
                openAwaiterFactory.SignalOpenChannel();
            }
            OnConnectionStatusChanged?.Invoke(Owner);
        }

    }
}
