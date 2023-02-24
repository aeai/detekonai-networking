using Detekonai.Core;
using Detekonai.Core.Common;
using Detekonai.Networking.Runtime.AsyncEvent;
using Detekonai.Networking.Runtime.Strategy;
using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Detekonai.Networking.Runtime.Tcp
{
    public class IdentityConnectionManager : ITcpConnectionManager
    {
        private readonly SocketAsyncEventArgsPool eventPool;
        private readonly IAsyncEventCommStrategy strategy;
        private readonly ICommChannelFactory<TcpChannel, IConnectionData> factory;
        private readonly BinaryBlobPool blobPool;
        private readonly ConcurrentDictionary<string, TcpChannel> channels = new ConcurrentDictionary<string, TcpChannel>();
        private readonly ConcurrentDictionary<string, CancellationTokenSource> ChannelsOnHold = new ConcurrentDictionary<string, CancellationTokenSource>();
        private readonly ConcurrentDictionary<Socket, object> externalData = new ConcurrentDictionary<Socket, object>();
        public event ITcpConnectionManager.ClientAccepted OnClientAccepted;
        public ILogger Logger { get; set; } = null;
        public int ReconnectTimeoutMillis { get; set; } = -1;
        public int IdTokenSize { get; set; } = 8;
        public IdentityConnectionManager(SocketAsyncEventArgsPool evPool, IAsyncEventCommStrategy eventHandlingStrategy, ICommChannelFactory<TcpChannel, IConnectionData> factory, BinaryBlobPool blobPool)
        {
            eventPool = evPool;
            strategy = eventHandlingStrategy;
            this.factory = factory;
            this.blobPool = blobPool;
        }

        private async void PurgeAfter(string id, int millis, CancellationToken token)
        {
            try
            {
                await Task.Delay(millis, token);
                if (!token.IsCancellationRequested)
                {
                    PurgeChannel(id);
                }
            }
            catch (OperationCanceledException)
            { }
        }

        public void PurgeChannel(string id)
        {
            if (channels.TryRemove(id, out TcpChannel val))
            {
                if (ChannelsOnHold.TryRemove(id, out CancellationTokenSource cts))
                {
                    cts.Dispose();
                }
                Logger?.Log(this, $"Channel-{id} is closed too long purging it...", ILogger.LogLevel.Verbose);
                val.Dispose();
            }
        }

        public void OnAccept(IConnectionData evt)
        {
            SocketAsyncEventArgs queryEvt = eventPool.Take(null, strategy, null, HandleEvent);
            eventPool.ConfigureSocketToRead(blobPool.GetBlob(), queryEvt, IdTokenSize);
            (queryEvt.UserToken as CommToken).ownerSocket = evt;
            if (!evt.Sock.ReceiveAsync(queryEvt))
            {
                strategy.EnqueueEvent(queryEvt);
            }
        }



        private void AssignChannel(SocketAsyncEventArgs e, string id)
        {
            TcpChannel ch = null;
            (e.UserToken as CommToken).ownerSocket.AddCustomData("session-id", id);
            if (channels.TryGetValue($"Ch-{id}", out ch))
            {
                Socket sock = (e.UserToken as CommToken).ownerSocket.Sock;
                ch.AssignSocket(sock);
                Logger?.Log(this, $"TCP Ch-{id} returned from {((IPEndPoint)sock.RemoteEndPoint).Address}:{((IPEndPoint)sock.RemoteEndPoint).Port}", ILogger.LogLevel.Verbose);
            }
            else
            {
                ch = factory.CreateFrom((e.UserToken as CommToken).ownerSocket);
                if (ch != null)
                {
                    ch.Name = $"Ch-{id}";
                    Socket sock = (e.UserToken as CommToken).ownerSocket.Sock;
                    channels.TryAdd(ch.Name, ch);
                    Logger?.Log(this, $"TCP Ch-{id} assigned to {((IPEndPoint)sock.RemoteEndPoint).Address}:{((IPEndPoint)sock.RemoteEndPoint).Port}", ILogger.LogLevel.Verbose);
                    OnClientAccepted?.Invoke(ch);
                    ch.Tactics.OnConnectionStatusChanged += Tactics_OnConnectionStatusChanged;
                }
                else
                {
                    Logger?.Log(this, $"Failed to initialize channel! ", ILogger.LogLevel.Error);
                    (e.UserToken as CommToken).ownerSocket.Sock.Shutdown(SocketShutdown.Both);
                    (e.UserToken as CommToken).ownerSocket.Sock.Close();
                }
            }
        }

        private void Tactics_OnConnectionStatusChanged(ICommChannel channel)
        {
            if (channel.Status == ICommChannel.EChannelStatus.Closed)
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
            else if (channel.Status == ICommChannel.EChannelStatus.Open)
            {
                if (ChannelsOnHold.TryRemove(channel.Name, out CancellationTokenSource cts))
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
                        AssignChannel(e, token.blob.ReadFixedString(IdTokenSize));
                    }
                }
                else
                {
                    if (e.UserToken is CommToken token)
                    {
                        token.ownerSocket.Sock.Dispose();
                    }
                    Logger?.Log(this, $"Error accepting socket identity: {e.SocketError}", ILogger.LogLevel.Error);

                }
            }
            eventPool.Release(e);
        }
    }
}