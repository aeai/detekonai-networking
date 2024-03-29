﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Detekonai.Core;
using Detekonai.Networking.Runtime.AsyncEvent;
using Detekonai.Networking.Runtime.Strategy;

namespace Detekonai.Networking.Runtime.Tcp
{
    public class SimpleTcpChannelFactory : ICommChannelFactory<TcpChannel, Socket>
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
        public TcpChannel Create()
        {
            TcpChannel channel = new TcpChannel(strategy, evtPool, blobPool);
            return channel;
        }

        public TcpChannel CreateFrom(Socket data)
        {
            TcpChannel channel = new TcpChannel(strategy, evtPool, blobPool);
            channel.AssignSocket(data);
            return channel;
        }
    }
}
