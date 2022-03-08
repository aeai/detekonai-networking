using Detekonai.Core.Common.Runtime.ThreadAgent;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;

namespace Detekonai.Networking.Runtime.AsyncEvent
{
    public class ChannelSyncronizationContext : SynchronizationContext, IThreadAgent
    {
        private BlockingCollection<KeyValuePair<SendOrPostCallback, object>> queue = new BlockingCollection<KeyValuePair<SendOrPostCallback, object>>();
        private readonly Thread thread;

        public ChannelSyncronizationContext(string name)
        {
            thread = new Thread(Loop)
            {
                Name = name,
                IsBackground = true
            };
            thread.Start();
        }

        public override void Post(SendOrPostCallback d, object state)
        {
            queue.Add(new KeyValuePair<SendOrPostCallback, object>(d, state));
        }

        public void Close()
        {
            queue.CompleteAdding();
        }

        private void Loop()
        {
            SynchronizationContext.SetSynchronizationContext(this);
            Console.WriteLine("Context start");
            while (!queue.IsAddingCompleted || queue.Count > 0)
            {
                var continuation = queue.Take();
                continuation.Key(continuation.Value);
            }

            Console.WriteLine("Context exit");
        }

        public void ExecuteOnThread(Action action)
        {
            Post((object ob) => action?.Invoke(), null);
        }
    }
}
