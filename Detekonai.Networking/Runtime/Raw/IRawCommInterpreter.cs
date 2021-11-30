using Detekonai.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Detekonai.Networking.Runtime.Raw
{
   
    public interface IRawCommInterpreter
    {
        /// <summary>
        /// Called when data arrived in raw mode to the channel
        /// </summary>
        /// <param name="channel">The channel this Interpreter is acting on</param>
        /// <param name="blob">The blob holding the data</param>
        /// <param name="bytesTransfered">The amount of data transfered</param>
        /// <returns>The amount of data we still waiting for or 0 if the current available data is fully interpreted </returns>
        int OnDataArrived(ICommChannel channel, BinaryBlob blob, int bytesTransfered);
    }
}
