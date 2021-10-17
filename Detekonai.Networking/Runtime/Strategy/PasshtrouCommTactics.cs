using Detekonai.Core;
using Detekonai.Networking.Runtime.AsyncEvent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Detekonai.Networking.Runtime.Strategy
{
    public class PasshtrouCommTactics : ICommTactics
    {
        private SingleThreadedChannelOpenAwaiterFactory openAwaiterFactory = null;
        private SingleThreadedBlobAwaiterFactory blobAwaiterFactory = null;

        public event ICommTactics.BlobReceivedHandler OnBlobReceived;
        public event ICommTactics.CommChannelChangeHandler OnRequestSent;
        public event ICommTactics.CommChannelChangeHandler OnConnectionStatusChanged;
        public event ICommTactics.TacticsCompleted OnTacticsCompleted;

        public ICommChannel Owner { get; private set; }
        public ICommTactics.RequestReceivedHandler RequestHandler { get ; set ; }
        public ICommTactics.RawDataReceiveHandler RawDataReceiver { get ; set ; }

        public CommTacticsFinalizerHelper TacticsFinalizer { get; private set; }

        public PasshtrouCommTactics(ICommChannel channel)
        {
            Owner = channel;
            openAwaiterFactory = new SingleThreadedChannelOpenAwaiterFactory(channel);
            blobAwaiterFactory = new SingleThreadedBlobAwaiterFactory(this, CancelRequest);
            TacticsFinalizer = new CommTacticsFinalizerHelper(() => { OnTacticsCompleted?.Invoke(this); });
        }
        
        public void Shutdown()
        {
            TacticsFinalizer.Execute();
        }

        private void CancelRequest(ushort idx)
        {
            blobAwaiterFactory.Cancel(idx);
        }

        public void CancelAllRequests()
        {
            blobAwaiterFactory.ScheduleCancelAll();
            openAwaiterFactory.Cancel();
            openAwaiterFactory.Invoke();
        }

        public IUniversalAwaiter<bool> CreateOpenAwaiter()
        {
            return openAwaiterFactory.Create();
        }

        public IUniversalAwaiter<ICommResponse> CreateResponseAwaiter(ushort messageIdx)
        {
            return blobAwaiterFactory.Create(messageIdx);
        }

        public void ReleaseResponse(ushort responseIdx)
        {
            blobAwaiterFactory.ReleaseResponse(responseIdx);
        }

        public void EnqueueResponse(ushort responseIdx, BinaryBlob blob)
        {
            blobAwaiterFactory.EnqueueResponse(responseIdx, blob);
        }

        public void BlobRecieved(BinaryBlob e)
        {
            int startIdx = e.Index;
            foreach (ICommTactics.BlobReceivedHandler del in OnBlobReceived.GetInvocationList())
            {
                e.Index = startIdx;
                del(Owner, e);
            }
        }

        public void RequestSent()
        {
            OnRequestSent?.Invoke(Owner);
        }

        public void StatusChanged()
        {
            if(Owner.Status == ICommChannel.EChannelStatus.Open)
            {
                openAwaiterFactory.SignalOpenChannel();
                openAwaiterFactory.Invoke();
            }
            OnConnectionStatusChanged?.Invoke(Owner);
        }
    }
}
