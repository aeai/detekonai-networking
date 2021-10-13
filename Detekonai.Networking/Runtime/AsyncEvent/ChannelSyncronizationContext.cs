using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Detekonai.Networking.Runtime.AsyncEvent
{
    public class ChannelSyncronizationContext : SynchronizationContext
    {
        private BlockingCollection<KeyValuePair<SendOrPostCallback, object>> queue = new BlockingCollection<KeyValuePair<SendOrPostCallback, object>>();
        private readonly Thread thread;
        
        public ChannelSyncronizationContext(string name)
        {
            thread = new Thread(Loop);
            thread.Name = name;
            thread.IsBackground = true;
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
    }
}
