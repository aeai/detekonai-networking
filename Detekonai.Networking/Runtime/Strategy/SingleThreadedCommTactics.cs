using Detekonai.Core;
using Detekonai.Core.Common.Runtime.ThreadAgent;
using Detekonai.Networking.Runtime.AsyncEvent;
using Detekonai.Networking.Runtime.Raw;

namespace Detekonai.Networking.Runtime.Strategy
{
    public class SingleThreadedCommTactics : ICommTactics
    {
        private SingleThreadedChannelOpenAwaiterFactory openAwaiterFactory = null;
        private SingleThreadedBlobAwaiterFactory blobAwaiterFactory = null;
        private readonly ManualThreadAgent agent = new ManualThreadAgent();
        public event ICommTactics.BlobReceivedHandler OnBlobReceived;
        public event ICommTactics.CommChannelChangeHandler OnRequestSent;
        public event ICommTactics.CommChannelChangeHandler OnConnectionStatusChanged;
        public event ICommTactics.TacticsCompleted OnTacticsCompleted;

        public ICommChannel Owner { get; private set; }
        public ICommTactics.RequestReceivedHandler RequestHandler { get; set; }
        public IRawCommInterpreter RawDataInterpreter { get; set; }

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
            openAwaiterFactory = new SingleThreadedChannelOpenAwaiterFactory(channel, agent);
            blobAwaiterFactory = new SingleThreadedBlobAwaiterFactory(this, agent);
            TacticsFinalizer = new CommTacticsFinalizerHelper(() => { status = EStatus.Finalized; OnTacticsCompleted?.Invoke(this); });
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
            blobAwaiterFactory.CancelAll();
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
                agent.ProcessAll();
            }
        }

        public void BlobRecieved(BinaryBlob e)
        {
            if (OnBlobReceived != null)
            {
                int startIdx = e.Index;
                foreach (ICommTactics.BlobReceivedHandler del in OnBlobReceived.GetInvocationList())
                {
                    e.Index = startIdx;
                    del(Owner, e);
                }
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
