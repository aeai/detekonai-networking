using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Detekonai.Core;
using Detekonai.Networking.Runtime.AsyncEvent;
using Detekonai.Networking.Runtime.Strategy;

namespace Detekonai.Networking.Runtime.Tcp
{
    public class SimpleTcpChannelFactory : ICommChannelFactory<TcpChannel>
    {
        private readonly SocketAsyncEventArgsPool evtPool;
        private readonly IAsyncEventCommStrategy strategy;
        private readonly BinaryBlobPool blobPool;
        public SimpleTcpChannelFactory(SocketAsyncEventArgsPool evtPool, IAsyncEventCommStrategy strategy, BinaryBlobPool blobPool)
        {
            this.evtPool = evtPool;
            this.strategy = strategy;
            this.blobPool = blobPool;
        }

        //class TestFinalizer : ICommTacticsFinalizer
        //{
        //    private readonly TcpChannel channel;

        //    public event ICommTacticsFinalizer.FinalizerDone OnDone;

        //    public TestFinalizer(TcpChannel channel)
        //    {
        //        this.channel = channel;
        //    }

        //    public async void Execute()
        //    {
        //        Console.WriteLine($"[{channel.Name}][{Thread.CurrentThread.Name}]Finalizer Start");
        //        await Task.Delay(2000);
        //        Console.WriteLine($"[{channel.Name}][{Thread.CurrentThread.Name}]Finalizer Middle");
        //        await Task.Delay(2000);
        //        Console.WriteLine($"[{channel.Name}][{Thread.CurrentThread.Name}]Finalizer End");
        //        OnDone?.Invoke();
        //    }

        //}

        public TcpChannel Create()
        {
            TcpChannel channel = new TcpChannel(strategy, evtPool, blobPool);
           // channel.Tactics.TacticsFinalizer.Value = new TestFinalizer(channel);
            return channel;
        }
    }
}
