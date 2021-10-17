using Detekonai.Core;
using Detekonai.Core.Common;
using Detekonai.Networking.Runtime.AsyncEvent;
using Detekonai.Networking.Runtime.Strategy;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Detekonai.Networking.Runtime.Tcp
{
    public class IdentityConnectionManager : ITcpConnectionManager
    {
        private readonly SocketAsyncEventArgsPool eventPool;
        private readonly IAsyncEventCommStrategy strategy;
        private readonly ICommChannelFactory<TcpChannel> factory;
        private readonly BinaryBlobPool blobPool;
        private readonly ConcurrentDictionary<string, TcpChannel> channels = new ConcurrentDictionary<string, TcpChannel>();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> ChannelsOnHold = new ConcurrentDictionary<string, CancellationTokenSource>();
        public event ITcpConnectionManager.ClientAccepted OnClientAccepted;
        public event ILogCapable.LogHandler Logger;
        public int ReconnectTimeoutMillis { get; set; } = -1;
        public IdentityConnectionManager(SocketAsyncEventArgsPool evPool, IAsyncEventCommStrategy eventHandlingStrategy, ICommChannelFactory<TcpChannel> factory, BinaryBlobPool blobPool)
        {
            eventPool = evPool;
            strategy = eventHandlingStrategy;
            this.factory = factory;
            this.blobPool = blobPool;
        }

        private async void PurgeAfter(string id, int millis, CancellationToken token)
        {
            await Task.Delay(millis, token);
            if(!token.IsCancellationRequested)
            {
                PurgeChannel(id);
            }
        }

        public void PurgeChannel(string id)
        {
           if(channels.TryRemove(id, out TcpChannel val))
            {
                if(ChannelsOnHold.TryRemove(id, out CancellationTokenSource cts))
                {
                    cts.Dispose();
                }
                Logger?.Invoke(this, $"Channel-{id} is closed too long purging it...", ILogCapable.LogLevel.Verbose);
                val.Dispose();
            }
        }

        public void OnAccept(SocketAsyncEventArgs evt)
        {
            SocketAsyncEventArgs queryEvt = eventPool.Take(null, strategy, null, HandleEvent);
            eventPool.ConfigureSocketToRead(blobPool.GetBlob(), queryEvt);
            (queryEvt.UserToken as CommToken).ownerSocket = evt.AcceptSocket;

            if (!evt.AcceptSocket.ReceiveAsync(queryEvt))
            {
                strategy.EnqueueEvent(queryEvt);
            }
        }

        private void AssignChannel(SocketAsyncEventArgs e, string id)
        {
            TcpChannel ch = null;
            if (channels.TryGetValue($"Ch-{id}", out ch))
            {
                Socket sock = (e.UserToken as CommToken).ownerSocket;
                ch.AssignSocket(sock);
                Logger?.Invoke(this, $"TCP Ch-{id} returned from {((IPEndPoint)e.RemoteEndPoint).Address}:{((IPEndPoint)e.RemoteEndPoint).Port}", ILogCapable.LogLevel.Verbose);
            }
            else
            {
                ch = factory.Create();
                ch.Name = $"Ch-{id}";
                ch.AssignSocket((e.UserToken as CommToken).ownerSocket);
                channels.TryAdd(ch.Name, ch);
                Logger?.Invoke(this, $"TCP Ch-{id} assigned to {((IPEndPoint)e.RemoteEndPoint).Address}:{((IPEndPoint)e.RemoteEndPoint).Port}", ILogCapable.LogLevel.Verbose);
                OnClientAccepted?.Invoke(ch);
                ch.Tactics.OnConnectionStatusChanged += Tactics_OnConnectionStatusChanged;
            }
        }

        private void Tactics_OnConnectionStatusChanged(ICommChannel channel)
        {
            if(channel.Status == ICommChannel.EChannelStatus.Closed)
            {
                if (ReconnectTimeoutMillis != -1)
                {
                    CancellationTokenSource src = new CancellationTokenSource();
                    if (ChannelsOnHold.TryAdd(channel.Name, src))
                    {
                        PurgeAfter(channel.Name, ReconnectTimeoutMillis, src.Token);
                    }
                    else
                    {
                        throw new InvalidOperationException($"Channel {channel.Name} is already in the cancellation list!");
                    }
                }
            }
            else if(channel.Status == ICommChannel.EChannelStatus.Open)
            {
               if(ChannelsOnHold.TryRemove(channel.Name, out CancellationTokenSource cts))
               {
                    cts.Cancel();
                    cts.Dispose();
               }
            }
        }

        private void HandleEvent(ICommChannel channel, BinaryBlob blob, SocketAsyncEventArgs e)
        {
            if (e.LastOperation == SocketAsyncOperation.Receive)
            {
                if (e.SocketError == SocketError.Success)
                {
                    if (e.UserToken is CommToken token)
                    {
                        AssignChannel(e, token.blob.ReadString());
                    }
                }
                else
                {
                    if (e.UserToken is CommToken token)
                    {
                        token.ownerSocket.Dispose();
                    }
                    Logger?.Invoke(this, $"Error accepting socket identity: {e.SocketError}", ILogCapable.LogLevel.Error);
                    
                }
            }
            eventPool.Release(e);
        }
    }
}