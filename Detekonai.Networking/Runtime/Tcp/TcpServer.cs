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
		private SocketAsyncEventArgsPool eventPool;
		private BinaryBlobPool bufferPool;

		private Socket serverSocket = null;
		private IPEndPoint tcpEndpoint;

		public ICommChannel.EChannelStatus Status { get; private set; } = ICommChannel.EChannelStatus.Closed;
		private IAsyncEventCommStrategy eventStrategy;
		private readonly ICommChannelFactory<TcpChannel> factory;
		
		public event ILogCapable.LogHandler Logger;
		public ITcpConnectionManager ConnectionManager { get; set; } = null;
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
		public TcpServer(int listeningPort, SocketAsyncEventArgsPool evPool, IAsyncEventCommStrategy eventHandlingStrategy, ICommChannelFactory<TcpChannel> factory, BinaryBlobPool blobPool)
		{
			eventPool = evPool;
			bufferPool = blobPool;
			tcpEndpoint = new IPEndPoint(IPAddress.Any, listeningPort);
			eventStrategy = eventHandlingStrategy;
			this.factory = factory;
			ConnectionManager = new DefaultConnectionManager(this, factory);
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

		//private void AssignChannel(SocketAsyncEventArgs e, string id)
  //      {
		//	Socket sock = (e.UserToken as CommToken).ownerSocket;
			
		//	Logger?.Invoke(this, $"Assigning id: {id}", ILogCapable.LogLevel.Verbose);
		//	TcpChannel ch = factory.Create();
		//	ch.Name = $"CLIENT:{((IPEndPoint)sock.RemoteEndPoint).Address}:{((IPEndPoint)sock.RemoteEndPoint).Port}";
		//	ch.AssignSocket(sock);
		//	OnClientAccepted?.Invoke(ch);
		//	Logger?.Invoke(this, $"TCP channel assigned {tcpEndpoint.Address}:{tcpEndpoint.Port} and {ch.Name}", ILogCapable.LogLevel.Verbose);
		//}

		public void HandleEvent(ICommChannel channel, BinaryBlob blob, SocketAsyncEventArgs e)
		{
			if(e.LastOperation == SocketAsyncOperation.Accept)
			{
				if (e.SocketError == SocketError.Success)
				{

					//if (reconnectPolicy != null)
					//{
					//	SocketAsyncEventArgs evt = eventPool.Take(null, eventStrategy, null, HandleEvent);
					//	Logger?.Invoke(this, $"Evt1 {evt.LastOperation}", ILogCapable.LogLevel.Verbose);
					//	eventPool.ConfigureSocketToRead(bufferPool.GetBlob(), evt);
					//	(evt.UserToken as CommToken).ownerSocket = e.AcceptSocket;
					//	if(!e.AcceptSocket.ReceiveAsync(evt))
					//                   {
					//		eventStrategy.EnqueueEvent(evt);
					//                   }
					//}
					//else
					//{
					//	AssignChannel(e, null);
					//}
					ConnectionManager.OnAccept(e);
					//Logger?.Invoke(this, $"TCP channel estabilished between host {tcpEndpoint.Address}:{tcpEndpoint.Port}", ILogCapable.LogLevel.Verbose);
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
