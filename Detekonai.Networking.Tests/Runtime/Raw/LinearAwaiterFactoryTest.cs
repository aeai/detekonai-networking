using Detekonai.Core.Common.Runtime.ThreadAgent;
using Detekonai.Networking.Runtime.AsyncEvent;
using Detekonai.Networking.Runtime.Raw;
using NUnit.Framework;
using System.Threading;
using System.Threading.Tasks;

namespace Detekonai.Networking.Tests.Runtime.Raw
{
    class LinearAwaiterFactoryTest
    {
        [Test]
        public async Task We_can_await()
        {
            LinearAwaiterFactory<string> factory = new LinearAwaiterFactory<string>(new ImmediateThreadAgent());
            var awaitable = new UniversalAwaitable<string>(factory.Create());

            Task.Run(async () =>
            {
                await Task.Delay(1000);
                factory.SetResponse("alma");
                factory.Continue();
            }
            );

            string response = await awaitable;

            Assert.That(response, Is.EqualTo("alma"));
        }

        [Test]
        public async Task We_can_timeout()
        {
            LinearAwaiterFactory<string> factory = new LinearAwaiterFactory<string>(new ImmediateThreadAgent());
            var awaitable = new UniversalAwaitable<string>(factory.Create());

            Task.Run(async () =>
            {
                await Task.Delay(10000);
                factory.SetResponse("alma");
                factory.Continue();
            }
            );

            using (var source = new CancellationTokenSource())
            {
                source.CancelAfter(1000);
                source.Token.Register(awaitable.CancelRequest);
                string response = await awaitable;
                Assert.That(response, Is.Null);
            }
        }

        [Test]
        public async Task We_can_finish_normaly_even_when_we_have_timout_set()
        {
            LinearAwaiterFactory<string> factory = new LinearAwaiterFactory<string>(new ImmediateThreadAgent());
            var awaitable = new UniversalAwaitable<string>(factory.Create());

            Task.Run(async () =>
            {
                await Task.Delay(1000);
                factory.SetResponse("alma");
                factory.Continue();
            }
            );

            using (var source = new CancellationTokenSource())
            {
                source.CancelAfter(2000);
                source.Token.Register(awaitable.CancelRequest);
                string response = await awaitable;
                Assert.That(response, Is.EqualTo("alma"));
            }

            await Task.Delay(2000);
        }
    }
}
