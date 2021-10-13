using Detekonai.Core;
using Detekonai.Core.Common;
using Detekonai.Networking.Runtime.AsyncEvent;
using Detekonai.Networking.Runtime.Strategy;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Detekonai.Networking.Runtime.Tcp
{
	public class TcpServer : ILogCapable
	{
		private readonly SocketAsyncEventArgsPool eventPool;

		private Socket serverSocket = null;
		private readonly IPEndPoint tcpEndpoint;

		public ICommChannel.EChannelStatus Status { get; private set; } = ICommChannel.EChannelStatus.Closed;
		private readonly IAsyncEventCommStrategy eventStrategy;	
		public event ILogCapable.LogHandler Logger;
		public ITcpConnectionManager ConnectionManager { get; set; } = null;

		public int ListeningPort
		{
			get
			{
				return serverSocket != null ? (serverSocket.LocalEndPoint as IPEndPoint).Port : tcpEndpoint.Port;
			}

		}

		public TcpServer(int listeningPort, SocketAsyncEventArgsPool evPool, IAsyncEventCommStrategy eventHandlingStrategy, ITcpConnectionManager manager)
		{
			eventPool = evPool;
			tcpEndpoint = new IPEndPoint(IPAddress.Any, listeningPort);
			eventStrategy = eventHandlingStrategy;
			ConnectionManager = manager;
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
			SocketAsyncEventArgs evt = eventPool.Take(null, eventStrategy, null, HandleEvent);
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
					ConnectionManager.OnAccept(e);
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
