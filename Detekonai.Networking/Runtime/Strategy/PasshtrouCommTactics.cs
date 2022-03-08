using Detekonai.Core;
using Detekonai.Core.Common.Runtime.ThreadAgent;
using Detekonai.Networking.Runtime.AsyncEvent;
using Detekonai.Networking.Runtime.Raw;

namespace Detekonai.Networking.Runtime.Strategy
{
    public class PasshtrouCommTactics : ICommTactics
    {
        private SingleThreadedChannelOpenAwaiterFactory openAwaiterFactory = null;
        private SingleThreadedBlobAwaiterFactory blobAwaiterFactory = null;
        private IThreadAgent agent = new ImmediateThreadAgent();
        public event ICommTactics.BlobReceivedHandler OnBlobReceived;
        public event ICommTactics.CommChannelChangeHandler OnRequestSent;
        public event ICommTactics.CommChannelChangeHandler OnConnectionStatusChanged;
        public event ICommTactics.TacticsCompleted OnTacticsCompleted;

        public ICommChannel Owner { get; private set; }
        public ICommTactics.RequestReceivedHandler RequestHandler { get; set; }
        public IRawCommInterpreter RawDataInterpreter { get; set; }

        public CommTacticsFinalizerHelper TacticsFinalizer { get; private set; }

        public PasshtrouCommTactics(ICommChannel channel)
        {
            Owner = channel;
            openAwaiterFactory = new SingleThreadedChannelOpenAwaiterFactory(channel, agent);
            blobAwaiterFactory = new SingleThreadedBlobAwaiterFactory(this, agent);
            TacticsFinalizer = new CommTacticsFinalizerHelper(() => { OnTacticsCompleted?.Invoke(this); });
        }

        public void Shutdown()
        {
            TacticsFinalizer.Execute();
        }

        public void CancelAllRequests()
        {
            blobAwaiterFactory.CancelAll();
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
            if (Owner.Status == ICommChannel.EChannelStatus.Open)
            {
                openAwaiterFactory.SignalOpenChannel();
            }
            OnConnectionStatusChanged?.Invoke(Owner);
        }
    }
}
