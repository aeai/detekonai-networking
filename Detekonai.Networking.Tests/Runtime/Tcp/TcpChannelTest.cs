using Detekonai.Core;
using Detekonai.Networking.Runtime.AsyncEvent;
using Detekonai.Networking.Runtime.Strategy;
using Detekonai.Networking.Runtime.Tcp;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Detekonai.Networking.Tests.Runtime.Tcp
{
    public class TcpChannelTest
    {
        [Test]
        public async Task TcpChannel_send_throtting_working()
        {
            ExclusiveThreadedCommStrategy strategy = new ExclusiveThreadedCommStrategy();
            
            SocketAsyncEventArgsPool evtPool = new SocketAsyncEventArgsPool();
            BinaryBlobPool blobPoolClient = new BinaryBlobPool(2, 16);
            BinaryBlobPool blobPoolServer = new BinaryBlobPool(2, 16);
            TcpServer server = new TcpServer(12345, evtPool, new PassthrouCommStrategy(), new DefaultConnectionManager(new ConnectionManagedTcpChannelFactory(evtPool, strategy, blobPoolServer)));
            server.ConnectionManager.OnClientAccepted += (TcpChannel client) => {
                client.Tactics.OnBlobReceived  +=  (ICommChannel channel, BinaryBlob e) =>
                {
                    Console.WriteLine(e.ReadString());
                    
                };
            };
            server.OpenChannel();
            TcpChannel client = new TcpChannel("127.0.0.1", 12345, strategy, evtPool, blobPoolClient);
            await client.OpenChannel();
            try
            {
                for (int i = 0; i < 5; i++)
                {
                    var msg = await client.CreateMessageWithSizeAsync(CancellationToken.None);
                    msg.AddString("alma");
                    client.Send(msg);
                }
            }catch(Exception ex)
            {
                Assert.Fail($"This should not throw, but we have a {ex.GetType()} exception: {ex.Message} {ex.StackTrace}");
            }

            await Task.Delay(5000);
            server.CloseChannel();
            client.CloseChannel();
        }

    }
}
