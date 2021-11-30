using Detekonai.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Detekonai.Networking.Runtime.Raw
{
    public class RawEventInterpreter : IRawCommInterpreter
    {
        public delegate int RawDataReceiveHandler(ICommChannel channel, BinaryBlob e, int bytesTransferred);

        public RawDataReceiveHandler Handler {get; set;}

        public int OnDataArrived(ICommChannel channel, BinaryBlob blob, int bytesTransfered)
        {
            return Handler == null ? 0 : Handler.Invoke(channel, blob, bytesTransfered);
        }
    }
}
