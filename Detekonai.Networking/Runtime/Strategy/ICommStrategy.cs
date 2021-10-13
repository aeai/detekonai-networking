using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Detekonai.Networking.Runtime.Strategy
{
    public interface ICommStrategy
    {
        ICommTactics RegisterChannel(ICommChannel channel);
    }
}
