using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Detekonai.Networking.Runtime.Strategy
{
    public class DefaultCommTacticsFinalizer : ICommTacticsFinalizer
    {
        public event ICommTacticsFinalizer.FinalizerDone OnDone;

        public void Execute()
        {
            OnDone?.Invoke();
        }
    }
}
