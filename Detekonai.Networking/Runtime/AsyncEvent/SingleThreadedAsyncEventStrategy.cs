using Detekonai.Core;
using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using static Detekonai.Networking.Runtime.AsyncEvent.IAsyncEventHandlingStrategy;

namespace Detekonai.Networking.Runtime.AsyncEvent
{
    public class SingleThreadedAsyncEventStrategy : IAsyncEventHandlingStrategy
	{
		private readonly ConcurrentQueue<SocketAsyncEventArgs> callbackQueue = new ConcurrentQueue<SocketAsyncEventArgs>();
        private Action OnProcess;

        public IAsyncEventHandlingTactics RegisterChannel(ICommChannel channel)
        {
            SingleThreadedAsyncEventTactics tac = new SingleThreadedAsyncEventTactics(channel);
            OnProcess += tac.Process;
            return tac;
        }

        public void UnregisterChannel(IAsyncEventHandlingTactics channelTactics)
        {
            
            OnProcess -= (channelTactics as SingleThreadedAsyncEventTactics).Process;
        }


        //private readonly ConcurrentQueue<ushort> cancelRequestQueue = new ConcurrentQueue<ushort>();
        //private readonly ConcurrentDictionary<ushort, CommResponse> awaiters = new ConcurrentDictionary<ushort, CommResponse>();

        //struct SingleThreadedBlobAwaiter : IUniversalAwaiter<ICommResponse>
        //{
        //    private readonly SingleThreadedAsyncEventStrategy owner;
        //    private readonly ushort idx;

        //    public SingleThreadedBlobAwaiter(SingleThreadedAsyncEventStrategy owner, ushort idx)
        //    {
        //        this.owner = owner;
        //        this.idx = idx;
        //        IsInitialized = false;
        //    }

        //    public bool IsCompleted { 
        //        get { 
        //            return owner.awaiters.TryGetValue(idx, out CommResponse cwr) && cwr.Status != ICommResponse.CommResponseStatus.Pending;
        //        }
        //    }

        //    public bool IsInitialized { get; private set; }

        //    public void Cancel()
        //    {
        //        owner.cancelRequestQueue.Enqueue(idx);
        //    }

        //    public ICommResponse GetResult()
        //    {
        //        owner.awaiters.TryGetValue(idx, out CommResponse val);
        //        return val;
        //    }

        //    public void OnCompleted(Action cont)
        //    {
        //        owner.awaiters[idx] = new CommResponse(idx, owner) { Continuation = cont };
        //        IsInitialized = true;
        //    }
        //}

        //struct ChannelOpenAwaiter : IUniversalAwaiter<bool>
        //{
        //    private readonly ICommChannel channel;

        //    public bool IsCompleted
        //    {
        //        get 
        //        {
        //            return 
        //        }
        //    }

        //    public bool IsInitialized { get; private set; }

        //    public ChannelOpenAwaiter(ICommChannel channel)
        //    {
        //        this.channel = channel;
        //        IsInitialized = false;
        //    }

        //    public void Cancel()
        //    {
        //       // channel.CloseChannel(); /threading, mind the threading!!!!
        //    }

        //    public bool GetResult()
        //    {
        //        return channel.Status == ICommChannel.EChannelStatus.Open;
        //    }

        //    public void OnCompleted(Action continuation)
        //    {
        //        IsInitialized = true;
        //    }
        //}

        //public void SignalOpenChannel(ICommChannel channel)
        //{
        //    throw new NotImplementedException(); ///HERE i want to wait for open too
        //}

        //public IUniversalAwaiter<bool> CreateOpenAwaiter(ICommChannel channel)
        //{
        //    return new ChannelOpenAwaiter(channel);
        //}

        //public IUniversalAwaiter<ICommResponse> CreateResponseAwaiter(ushort messageIdx)
        //{
        //    return new SingleThreadedBlobAwaiter(this, messageIdx);
        //}


        public void EnqueueEvent(SocketAsyncEventArgs evt)
		{
			callbackQueue.Enqueue(evt);
		}

        public void Process()
		{
			while (!callbackQueue.IsEmpty)
			{
				if (callbackQueue.TryDequeue(out SocketAsyncEventArgs e))
				{
					if (e.UserToken is CommToken comm)
					{
						comm.callback(comm.owner, comm.blob, e);
					}
				}
			}


            OnProcess?.Invoke();
            //while (!cancelRequestQueue.IsEmpty)
            //{
            //    if (cancelRequestQueue.TryDequeue(out ushort idx))
            //    {
            //        if (awaiters.TryGetValue(idx, out CommResponse cwr))
            //        { 
            //            cwr.Status = ICommResponse.CommResponseStatus.Canceled;
            //            cwr.Continuation?.Invoke();
            //        }
            //    }
            //}
        }

        //public void ReleaseResponse(ushort responseIdx)
        //{
        //    awaiters.TryRemove(responseIdx, out CommResponse r);
        //}

        //public void EnqueueResponse(ushort responseIdx, BinaryBlob blob)
        //{
        //    if (awaiters.TryGetValue(responseIdx, out CommResponse cwr))
        //    {
        //        cwr.Blob = blob;
        //        cwr.Status = ICommResponse.CommResponseStatus.Finished;
        //        cwr.Continuation?.Invoke();      
        //    }
        //}

        //public void CancelAllRequests()
        //{
        //    foreach(ushort cr in awaiters.Keys)
        //    {
        //        cancelRequestQueue.Enqueue(cr);
        //    }
        //}

    }
}
