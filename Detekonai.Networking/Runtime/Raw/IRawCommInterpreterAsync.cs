﻿using Detekonai.Core;
using Detekonai.Networking.Runtime.AsyncEvent;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Detekonai.Networking.Runtime.Raw
{
    public interface IContinuable
    {
        void Continue();
    }

    public interface IRawCommInterpreterAsync<T> : IRawCommInterpreter
    {
        UniversalAwaitable<T> SendRpc(ICommChannel channel, BinaryBlob blob);
        UniversalAwaitable<T> AwaitData();
        UniversalAwaitable<T> SendRpc(ICommChannel channel, BinaryBlob blob, CancellationToken token);
        UniversalAwaitable<T> AwaitData(CancellationToken token);
    }
}
