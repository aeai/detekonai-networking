using Detekonai.Core;
using Detekonai.Networking.Runtime.AsyncEvent;
using Detekonai.Networking.Runtime.Strategy;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Detekonai.Networking.Tests.Runtime.Strategy
{
    class GenericCommTacticsTest
    {

        private static ICommChannel channel = Substitute.For<ICommChannel>();

        public static IEnumerable<ICommTactics> GenerateTactics() {
            yield return new PasshtrouCommTactics(channel);
            yield return new SingleThreadedCommTactics(channel);
            yield return new ExclusiveThreadedCommTactics(channel, "test");
        }

        private ICommTactics[] tactics;
        private BinaryBlobPool pool = new BinaryBlobPool(5, 16);
        [Test]
        [TestCaseSource(nameof(GenerateTactics))]
        public void BlobReceived_getting_called(ICommTactics tactics)
        {
            bool called = false;
            void Tactics_OnBlobReceived(ICommChannel channel, Core.BinaryBlob e)
            {
                called = true;
                Assert.That(e, Is.Not.Null);
            }

            BinaryBlob blob = pool.GetBlob();
            tactics.OnBlobReceived += Tactics_OnBlobReceived;
            tactics.BlobRecieved(blob);
            blob.Release();
            Assert.That(pool.AvailableChunks, Is.EqualTo(5));
            Assert.That(called, Is.True);
        }
        [Test]
        [TestCaseSource(nameof(GenerateTactics))]
        public void BlobReceived_works_with_multiple_receivers_always_start_in_the_same_index(ICommTactics tactics)
        {
            void Tactics_OnBlobReceived(ICommChannel channel, Core.BinaryBlob e)
            {
                Assert.That(e.ReadInt(), Is.EqualTo(12345));
            }
            
            void Tactics_OnBlobReceived2(ICommChannel channel, Core.BinaryBlob e)
            {
                Assert.That(e.ReadInt(), Is.EqualTo(12345));
            }

            BinaryBlob blob = pool.GetBlob();
            blob.AddShort(456);
            blob.AddInt(12345);
            blob.Index = 2;
            tactics.OnBlobReceived += Tactics_OnBlobReceived;
            tactics.OnBlobReceived += Tactics_OnBlobReceived2;
            tactics.BlobRecieved(blob);
            blob.Release();
            Assert.That(pool.AvailableChunks, Is.EqualTo(5));
        }

        [Test]
        [TestCaseSource(nameof(GenerateTactics))]
        public void StatusChanged_called(ICommTactics tactics)
        {
            ICommChannel.EChannelStatus status = ICommChannel.EChannelStatus.Closed;
            void Tactics_OnConnectionStatusChanged(ICommChannel channel)
            {
               status = channel.Status;
            }

            tactics.OnConnectionStatusChanged += Tactics_OnConnectionStatusChanged;
            channel.Status.Returns(ICommChannel.EChannelStatus.Open);
            tactics.StatusChanged();

           Assert.That(status, Is.EqualTo(ICommChannel.EChannelStatus.Open));
        }

        [Test]
        [TestCaseSource(nameof(GenerateTactics))]
        public void RequestSent_getting_called(ICommTactics tactics)
        {
            bool called = false;
            void Tactics_OnRequestSent(ICommChannel channel)
            {
                called = true;
            }
           
            tactics.OnRequestSent += Tactics_OnRequestSent;
            tactics.RequestSent();
            Assert.That(called, Is.True);
        }


        [Test]
        [TestCaseSource(nameof(GenerateTactics))]
        public async Task CreateResponseAwaiter_we_can_wait_for_requests(ICommTactics tactics)
        {
            Task.Run(async () =>
                {
                    await Task.Delay(1000);
                    var blob = pool.GetBlob();
                    blob.AddInt(1234);
                    blob.JumpIndexToBegin();
                    tactics.EnqueueResponse(1, blob);
                }
            );

            using (var ret = await new UniversalAwaitable<ICommResponse>(tactics.CreateResponseAwaiter(1)))
            {
                Assert.That(ret.Blob.ReadInt(), Is.EqualTo(1234));
            }
            Assert.That(pool.AvailableChunks,Is.EqualTo(5));
        }

        [Test]
        public async Task PasshtrouCommTactics_CreateOpenAwaiter_we_can_wait_for_requests()
        {
            var tactics = new PasshtrouCommTactics(channel);
            channel.Status.Returns(ICommChannel.EChannelStatus.Closed);
            Task.Run(async () =>
            {
                await Task.Delay(1000);
                channel.Status.Returns(ICommChannel.EChannelStatus.Open);
                tactics.StatusChanged();
            }
            );

            var ret = await new UniversalAwaitable<bool>(tactics.CreateOpenAwaiter());

            Assert.That(ret, Is.True);
            Assert.That(pool.AvailableChunks, Is.EqualTo(5));
        }

        [Test]
        public async Task SingleThreadedCommTactics_CreateOpenAwaiter_we_can_wait_for_requests()
        {
            var tactics = new SingleThreadedCommTactics(channel);
            channel.Status.Returns(ICommChannel.EChannelStatus.Closed);
            Thread startThread  = null;
            Task.Run(async () =>
            {
                await Task.Delay(1000);
                startThread = Thread.CurrentThread;
                channel.Status.Returns(ICommChannel.EChannelStatus.Open);
                tactics.StatusChanged();
                tactics.Process();
            }
            );

            var ret = await new UniversalAwaitable<bool>(tactics.CreateOpenAwaiter());

            Assert.That(ret, Is.True);
            Assert.That(startThread, Is.EqualTo(Thread.CurrentThread), "We need to continue the same thread we called Process");
            Assert.That(pool.AvailableChunks, Is.EqualTo(5));
        }

        [Test]
        public async Task ExclusiveThreadedCommTactics_CreateOpenAwaiter_we_can_wait_for_requests()
        {
            var tactics = new ExclusiveThreadedCommTactics(channel, "test");
            channel.Status.Returns(ICommChannel.EChannelStatus.Closed);
            Task.Run(async () =>
            {
                await Task.Delay(1000);
                channel.Status.Returns(ICommChannel.EChannelStatus.Open);
                
                tactics.Post((object ob) =>
                {  
                    tactics.StatusChanged();
                }, null);
            });
            var ret = await new UniversalAwaitable<bool>(tactics.CreateOpenAwaiter());
            Thread startThread = Thread.CurrentThread;
            await Task.Delay(1000);
            Assert.That(ret, Is.True);
            Assert.That(startThread, Is.EqualTo(Thread.CurrentThread), "We need to continue the same thread");
            Assert.That(pool.AvailableChunks, Is.EqualTo(5));
        }
    }
}
