using Detekonai.Core;
using Detekonai.Core.Common;
using Detekonai.Networking.Runtime.AsyncEvent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Detekonai.Networking.Runtime
{
	public class TcpServer : ILogCapable
	{


		//private List<TcpChannel> clients = new List<TcpChannel>();

		private SocketAsyncEventArgsPool eventPool;
		private BinaryBlobPool bufferPool;

		private Socket serverSocket = null;
		private IPEndPoint tcpEndpoint;

		public delegate void ClientAccepted(TcpChannel client);
		public event ClientAccepted OnClientAccepted;
		public event ILogCapable.LogHandler Logger;
		public ICommChannel.EChannelStatus Status { get; private set; } = ICommChannel.EChannelStatus.Closed;
		public IAsyncEventHandlingStrategy eventStrategy;
        private readonly ICommChannelFactory<TcpChannel> factory;

        public int ListeningPort
		{
			get
			{
				return serverSocket != null ? (serverSocket.LocalEndPoint as IPEndPoint).Port : tcpEndpoint.Port;
			}

		}
		public string Name
		{
			get
			{
				return "Server";
			}
		}
		public TcpServer(int listeningPort, SocketAsyncEventArgsPool evPool, IAsyncEventHandlingStrategy eventHandlingStrategy, ICommChannelFactory<TcpChannel> factory, BinaryBlobPool blobPool)
		{
			eventPool = evPool;
			bufferPool = blobPool;
			tcpEndpoint = new IPEndPoint(IPAddress.Any, listeningPort);
			eventStrategy = eventHandlingStrategy;
            this.factory = factory;
		}

		public void CloseChannel()
		{
			serverSocket?.Close();
			serverSocket?.Dispose();
			serverSocket = null;
			Logger?.Invoke(this, $"TCP server closed {tcpEndpoint.Address} and port {tcpEndpoint.Port}", ILogCapable.LogLevel.Info);
			Status = ICommChannel.EChannelStatus.Closed;
		}

		/// <summary>
		/// Opens the server
		/// </summary>
		/// <exception cref="System.Net.Sockets.SocketException">If something went wrong</exception>
		/// <returns>True if the channel opens</returns>
		public void OpenChannel()
		{
			serverSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
			serverSocket.Bind(tcpEndpoint);
			serverSocket.Listen(10000);
			Logger?.Invoke(this, $"TCP channel opening for host {tcpEndpoint.Address} and port {tcpEndpoint.Port}", ILogCapable.LogLevel.Info);
			Status = ICommChannel.EChannelStatus.Open;
			Accept();
		}

		private void Accept()
		{
			TcpChannel channel = factory.Create();
			SocketAsyncEventArgs evt = eventPool.Take(channel, eventStrategy, HandleEvent);
			evt.AcceptSocket = null;
			if (serverSocket != null)
			{
				if (!serverSocket.AcceptAsync(evt))
				{
					eventStrategy.EnqueueEvent(evt);
				}
			}
		}

		public void Dispose()
		{
			Logger?.Invoke(this, $"TCP channel disposed for host {tcpEndpoint.Address.ToString()} and port {tcpEndpoint.Port}", ILogCapable.LogLevel.Info);
			CloseChannel();
		}

		public void HandleEvent(ICommChannel channel, BinaryBlob blob, SocketAsyncEventArgs e)
		{
			if(e.LastOperation == SocketAsyncOperation.Accept)
			{
				if (e.SocketError == SocketError.Success)
				{
					if (channel is TcpChannel ch)
					{
						ch.Name = $"CLIENT:{((IPEndPoint)e.AcceptSocket.RemoteEndPoint).Address}:{((IPEndPoint)e.AcceptSocket.RemoteEndPoint).Port}";
						ch.AssignSocket(e.AcceptSocket);
						OnClientAccepted?.Invoke(ch);
						Logger?.Invoke(this, $"TCP channel estabilished between host {tcpEndpoint.Address}:{tcpEndpoint.Port} and {ch.Name}", ILogCapable.LogLevel.Verbose);
					}
				}
				else
                {
					Logger?.Invoke(this, $"Error accepting socket: {e.SocketError}", ILogCapable.LogLevel.Error);
				}
				Accept();
			}

			eventPool.Release(e);
		}
	}
}
