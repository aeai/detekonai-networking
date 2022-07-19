using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Detekonai.Networking.Runtime.Strategy
{
    public class CommTacticsFinalizerHelper
    {
        private ICommTacticsFinalizer tacticsFinalizer;
        public ICommTacticsFinalizer Value
        {
            get
            {
                return tacticsFinalizer;
            }
            set
            {
                if (tacticsFinalizer != null)
                {
                    tacticsFinalizer.OnDone -= doneCallback;
                }
                if (value == null)
                {
                    tacticsFinalizer = new DefaultCommTacticsFinalizer();
                }
                else
                {
                    tacticsFinalizer = value;
                }
                tacticsFinalizer.OnDone += doneCallback;
            }
        }
        private ICommTacticsFinalizer.FinalizerDone doneCallback;

        public void Execute()
        {
            Value.Execute();
        }

        public CommTacticsFinalizerHelper(ICommTacticsFinalizer.FinalizerDone doneCallback)
        {
            this.doneCallback = doneCallback;
            Value = new DefaultCommTacticsFinalizer();
        }
    }
}
