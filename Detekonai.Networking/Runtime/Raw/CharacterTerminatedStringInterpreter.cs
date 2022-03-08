using Detekonai.Core;
using Detekonai.Core.Common.Runtime.ThreadAgent;
using Detekonai.Networking.Runtime.AsyncEvent;
using System;
using System.Threading;

namespace Detekonai.Networking.Runtime.Raw
{
    public class CharacterTerminatedStringInterpreter : IRawCommInterpreterAsync<string>, IContinuable
    {
        private readonly LinearAwaiterFactory<string> awaiterFactory;

        private int dataCounter = 0;

        public char Terminator { get; }

        public int OnDataArrived(ICommChannel channel, BinaryBlob blob, int bytesTransfered)
        {
            dataCounter += bytesTransfered;
            blob.Index = dataCounter - 1;
            if (blob.ReadByte() == Terminator)
            {
                blob.JumpIndexToBegin();
                awaiterFactory.SetResponse(blob.ReadFixedString(dataCounter));
                dataCounter = 0;
                return 0;
            }
            else if (blob.Index == blob.BufferSize)
            {
                throw new IndexOutOfRangeException("We ran out of buffer space!");
            }
            else
            {
                return blob.BufferSize - blob.Index;
            }
        }

        public CharacterTerminatedStringInterpreter(char terminator, IThreadAgent threadAgent)
        {
            awaiterFactory = new LinearAwaiterFactory<string>(threadAgent);
            Terminator = terminator;
        }

        public UniversalAwaitable<string> AwaitData()
        {
            IUniversalAwaiter<string> awaiter = awaiterFactory.Create();
            return new UniversalAwaitable<string>(awaiter);
        }

        public UniversalAwaitable<string> SendRpc(ICommChannel channel, BinaryBlob blob)
        {
            IUniversalAwaiter<string> awaiter = awaiterFactory.Create();
            channel.Send(blob);
            return new UniversalAwaitable<string>(awaiter);
        }

        public void Continue()
        {
            awaiterFactory.Continue();
        }

        public UniversalAwaitable<string> SendRpc(ICommChannel channel, BinaryBlob blob, CancellationToken token)
        {
            UniversalAwaitable<string> res = SendRpc(channel, blob);
            token.Register(res.CancelRequest);
            return res;
        }
        public UniversalAwaitable<string> AwaitData(CancellationToken token)
        {
            UniversalAwaitable<string> res = AwaitData();
            token.Register(res.CancelRequest);
            return res;
        }
    }
}
