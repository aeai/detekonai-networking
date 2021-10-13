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
    public class SingleThreadedCommTactics : ICommTactics
    {
        private readonly ConcurrentQueue<ushort> cancelRequestQueue = new ConcurrentQueue<ushort>();
        private SingleThreadedChannelOpenAwaiterFactory openAwaiterFactory = null;
        private SingleThreadedBlobAwaiterFactory blobAwaiterFactory = null;

        public event ICommTactics.BlobReceivedHandler OnBlobReceived;
        public event ICommTactics.CommChannelChangeHandler OnRequestSent;
        public event ICommTactics.CommChannelChangeHandler OnConnectionStatusChanged;
        public event ICommTactics.TacticsCompleted OnTacticsCompleted;

        public ICommChannel Owner { get; private set; }
        public ICommTactics.RequestReceivedHandler RequestHandler { get; set ; }
        public ICommTactics.RawDataReceiveHandler RawDataReceiver { get; set ; }
 
        private enum EStatus 
        {
            Active,
            PreCleanUp,
            CleanUp,
            Finalized
        };
        private EStatus status = EStatus.Active;
        public CommTacticsFinalizerHelper TacticsFinalizer { get; private set; }

        public void Shutdown()
        {
            if (status == EStatus.Active)
            {
                status = EStatus.PreCleanUp;
            }
        }

        public SingleThreadedCommTactics(ICommChannel channel)
        {
            Owner = channel;
            openAwaiterFactory = new SingleThreadedChannelOpenAwaiterFactory(channel);
            blobAwaiterFactory = new SingleThreadedBlobAwaiterFactory(this, CancelRequest);
            TacticsFinalizer = new CommTacticsFinalizerHelper(() => { status = EStatus.Finalized; OnTacticsCompleted?.Invoke(this); });
        }

        private void CancelRequest(ushort idx)
        {
            cancelRequestQueue.Enqueue(idx);
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

        public void CancelAllRequests()
        {
            blobAwaiterFactory.ScheduleCancelAll();
            openAwaiterFactory.Cancel();
        }



        public void Process()
        {
            if (status == EStatus.Finalized)
            {
                return;
            }
            else if (status == EStatus.PreCleanUp)
            {
                TacticsFinalizer.Execute();
                status = EStatus.CleanUp;
            }
            else
            {
                while (!cancelRequestQueue.IsEmpty)
                {
                    if (cancelRequestQueue.TryDequeue(out ushort idx))
                    {
                        blobAwaiterFactory.Cancel(idx);
                    }
                }

                if (openAwaiterFactory.Finished)
                {
                    openAwaiterFactory.Invoke();
                }
            }
        }

        public void BlobRecieved(BinaryBlob e)
        {
            OnBlobReceived?.Invoke(Owner, e);
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
