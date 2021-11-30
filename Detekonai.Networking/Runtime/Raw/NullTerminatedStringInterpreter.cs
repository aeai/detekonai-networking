using Detekonai.Core;
using Detekonai.Networking.Runtime.AsyncEvent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Detekonai.Networking.Runtime.Raw
{
    public class NullTerminatedStringInterpreter : IRawCommInterpreterAsync<string>
    {
        private SingletonAwaiterFactory<string> awaiterFactory = new SingletonAwaiterFactory<string>();

        private int dataCounter = 0;
        public int OnDataArrived(ICommChannel channel, BinaryBlob blob, int bytesTransfered)
        {
            dataCounter += bytesTransfered;
            blob.Index = dataCounter - 1;
            if(blob.ReadByte() == 0)
            {
                blob.JumpIndexToBegin();
                awaiterFactory.SetResponse(blob.ReadFixedString(dataCounter));
                dataCounter = 0;
                return 0;
            }
            else if(blob.Index == blob.BufferSize)
            {
                throw new IndexOutOfRangeException("We ran out of buffer space!");
            }
            else
            {
                return blob.BufferSize - blob.Index;
            }
        }

        public UniversalAwaitable<string> SendRpc(ICommChannel channel, BinaryBlob blob)
        {
            IUniversalAwaiter<string> awaiter = awaiterFactory.Create();
            channel.Send(blob);
            return new UniversalAwaitable<string>(awaiter); ;
        }

    }
}
