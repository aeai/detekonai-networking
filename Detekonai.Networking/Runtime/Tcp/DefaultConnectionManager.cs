﻿using Detekonai.Core.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Detekonai.Networking.Runtime.Tcp
{
    public class DefaultConnectionManager : ITcpConnectionManager
    {
        private readonly ICommChannelFactory<TcpChannel> factory;

        public event ITcpConnectionManager.ClientAccepted OnClientAccepted;

        private int counter = 0;

        public ILogConnector Logger { get; set; } = null;

        public DefaultConnectionManager(ICommChannelFactory<TcpChannel> factory)
        {
            this.factory = factory;
        }

        public void OnAccept(SocketAsyncEventArgs evt)
        {
            int id = Interlocked.Increment(ref counter);
            TcpChannel ch = factory.Create();
            ch.Name = $"Channel-{id}";
            ch.AssignSocket(evt.AcceptSocket);
            OnClientAccepted?.Invoke(ch);
            Logger?.Log(this, $"TCP Channel-{id} assigned to {((IPEndPoint)evt.AcceptSocket.RemoteEndPoint).Address}:{((IPEndPoint)evt.AcceptSocket.RemoteEndPoint).Port}", ILogConnector.LogLevel.Verbose);
        }

    }
}
