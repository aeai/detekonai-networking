using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Detekonai.Networking.Runtime.Strategy
{
    public interface ICommTacticsFinalizer
    {
       public delegate void FinalizerDone();
       event FinalizerDone OnDone;
       void Execute();
    }
}
