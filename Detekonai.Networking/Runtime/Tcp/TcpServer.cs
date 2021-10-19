using Detekonai.Core;
using Detekonai.Core.Common;
using Detekonai.Networking.Runtime.AsyncEvent;
using Detekonai.Networking.Runtime.Strategy;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;

namespace Detekonai.Networking.Runtime.Tcp
{
	public class TcpServer
	{
		private readonly SocketAsyncEventArgsPool eventPool;

		private Socket serverSocket = null;
		private readonly IPEndPoint tcpEndpoint;

		public ICommChannel.EChannelStatus Status { get; private set; } = ICommChannel.EChannelStatus.Closed;
		private readonly IAsyncEventCommStrategy eventStrategy;
		public ILogConnector Logger { get; set; }
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
			Logger?.Log(this, $"TCP server closed {tcpEndpoint.Address} and port {tcpEndpoint.Port}", ILogConnector.LogLevel.Info);
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
			Logger?.Log(this, $"TCP channel opening for host {tcpEndpoint.Address} and port {tcpEndpoint.Port}", ILogConnector.LogLevel.Info);
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
			Logger?.Log(this, $"TCP channel disposed for host {tcpEndpoint.Address.ToString()} and port {tcpEndpoint.Port}", ILogConnector.LogLevel.Info);
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
					Logger?.Log(this, $"Error accepting socket: {e.SocketError}", ILogConnector.LogLevel.Error);
				}
				Accept();
			}
			eventPool.Release(e);
		}
	}
}
