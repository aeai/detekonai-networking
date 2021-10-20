using Detekonai.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Detekonai.Networking.Runtime
{
    public interface IRequestTicket
    {
        void Fulfill(BinaryBlob blob);
    }
}
