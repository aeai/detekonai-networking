using Detekonai.Core;
using Detekonai.Core.Common;
using Detekonai.Networking.Runtime.AsyncEvent;
using Detekonai.Networking.Runtime.Raw;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using static Detekonai.Core.Common.ILogger;
using static Detekonai.Networking.ICommChannel;

namespace Detekonai.Networking.Runtime.Tcp
{
    public class TcpPacketBuilder
    {
		private const ushort headerSize = 6;
		private enum EReadingMode
		{
			Header,
			Data,
		}

		public ILogger Logger { get; set; }

		private EReadingMode readingMode = EReadingMode.Header;
		private int bytesNeeded = headerSize;

		public enum EPackageAction 
		{
			EndOfStream,
			Done,
			Processing
		}

		//TODO separate from TCPChannel so it don't need to be public
		public interface ITcpPacketHandler 
		{
			void ContinueReceivingData(int size, BinaryBlob blob, SocketAsyncEventArgs eventArgs);

			BinaryBlob GetBlobFromPool(int size);

			int RawPoolSize { get; }

			void CommReceived(CommToken token);

			void EndOfStream();
		}

		private bool modeTransition = false;
		private ICommChannel.EChannelMode mode = ICommChannel.EChannelMode.Managed;
		public ICommChannel.EChannelMode Mode
		{
			get
			{
				return mode;
			}
			set
			{
				if (mode == EChannelMode.Raw && value == EChannelMode.Managed)
				{
					modeTransition = true;
				}
				mode = value;
			}
		}


		private ITcpPacketHandler handler;

		public static void AddHeader(BinaryBlob blob, CommToken.HeaderFlags flags, uint size, ushort index)
		{
			uint flagAndSize = (uint)((byte)flags << 24);
			flagAndSize |= (uint)(size);
			blob.AddUInt(flagAndSize);
			blob.AddUShort(index);
		}

		public TcpPacketBuilder(ITcpPacketHandler handler)
        {
			this.handler = handler;
        }

		public void Receive(CommToken token, int bytesTransferred, SocketAsyncEventArgs eventArgs)
        {
			if(Mode == EChannelMode.Managed)
            {
				 ManagedReceive(token, bytesTransferred, eventArgs);
            }
			else
            {
				 HandleRawReceive(token, bytesTransferred, eventArgs);
            }
        }

		private void ManagedReceive(CommToken token, int bytesTransferred, SocketAsyncEventArgs eventArgs)
        {
			if (modeTransition)
			{
				modeTransition = false;
				HandleSlowReceive(token, bytesTransferred, eventArgs);
			}
			else 
			{
				HandleManagedReceive(token, bytesTransferred, eventArgs);
			}
        }

		private void HandleManagedReceive(CommToken token, int bytesTransferred, SocketAsyncEventArgs e)
		{
			var blob = token.blob;

			if (bytesTransferred == 0)
			{
				handler.EndOfStream();
			}
			else
			{
				bytesNeeded -= bytesTransferred;
				if (bytesNeeded == 0)
				{
					blob.JumpIndexToBegin();
					if (readingMode == EReadingMode.Header)
					{
						readingMode = EReadingMode.Data;
						uint flagsAndSize = blob.ReadUInt();
						token.headerFlags = ((CommToken.HeaderFlags)((flagsAndSize & 0xFF000000) >> 24));
						bytesNeeded = (int)(flagsAndSize & 0x00FFFFFF);
						token.msgSize = bytesNeeded;
						token.index = blob.ReadUShort();
						blob.JumpIndexToBegin();
						if(bytesNeeded == 0)
                        {
							handler.CommReceived(token);
							readingMode = EReadingMode.Header;
							bytesNeeded = headerSize;
							token.blob?.Release();
							handler.ContinueReceivingData(bytesNeeded, handler.GetBlobFromPool(headerSize), e);
						}
						else if (bytesNeeded > blob.BufferSize)
						{
							blob.Release();
							handler.ContinueReceivingData(bytesNeeded, handler.GetBlobFromPool(bytesNeeded), e);
						}
						else
						{
							handler.ContinueReceivingData(bytesNeeded, blob, e);
						}
					}
					else
					{
						handler.CommReceived(token);

						readingMode = EReadingMode.Header;
						bytesNeeded = headerSize;
						token.blob?.Release();
						handler.ContinueReceivingData(bytesNeeded, handler.GetBlobFromPool(headerSize), e);
					}
				}
				else
				{
					handler.ContinueReceivingData(bytesNeeded, blob, e);
				}
			}
		}

		private void HandleSlowReceive(CommToken token, int bytesTransferred, SocketAsyncEventArgs e)
		{
			var blob = token.blob;

			if (bytesTransferred == 0)
			{
				handler.EndOfStream();
			}
			else
			{
				int availableBytes = bytesTransferred;
				if (availableBytes < headerSize) //we have less data that needed for a header, go back to fast track
				{
					bytesNeeded = headerSize - availableBytes;
					handler.ContinueReceivingData(bytesNeeded, blob, e);
				}
				else
				{// slow track we may have more than 1 message or partial headers etc.
					blob.JumpIndexToBegin();
					while (availableBytes > 0)
					{
						if (availableBytes < headerSize) //we have a partial header, create new blob release old one
						{
							bytesNeeded = headerSize - availableBytes;
							BinaryBlob msgBlob = handler.GetBlobFromPool(headerSize);
							msgBlob.CopyDataFrom(blob, availableBytes);
							availableBytes = 0;
							blob.Release();
							readingMode = EReadingMode.Header;
							handler.ContinueReceivingData(bytesNeeded, msgBlob, e);
						}
						else if (availableBytes >= headerSize) //we have header and extra data
						{
							availableBytes -= headerSize;
							uint flagsAndSize = blob.ReadUInt();
							token.headerFlags = ((CommToken.HeaderFlags)((flagsAndSize & 0xFF000000) >> 24));
							bytesNeeded = (int)(flagsAndSize & 0x00FFFFFF);
							token.msgSize = bytesNeeded;
							token.index = blob.ReadUShort();
							readingMode = EReadingMode.Data;

							if (bytesNeeded <= availableBytes) // we have enough data for a complete message
							{
								BinaryBlob msgBlob = handler.GetBlobFromPool(bytesNeeded);
								msgBlob.CopyDataFrom(blob, bytesNeeded);
								availableBytes -= bytesNeeded;

								msgBlob.JumpIndexToBegin();
								token.blob = msgBlob;
								handler.CommReceived(token);
								token.blob?.Release();
								if(availableBytes == 0)// no more data to process, start waiting for new header
                                {
									readingMode = EReadingMode.Header;
									bytesNeeded = headerSize;
									blob.Release();
									handler.ContinueReceivingData(bytesNeeded, handler.GetBlobFromPool(headerSize), e);
								}
							}
							else if (bytesNeeded >= availableBytes)//we continue requesting data in the normal handler
							{
								BinaryBlob msgBlob = handler.GetBlobFromPool(bytesNeeded);
								msgBlob.CopyDataFrom(blob, availableBytes);
								bytesNeeded -= availableBytes;
								availableBytes = 0;
								blob.Release();
								readingMode = EReadingMode.Data;
								handler.ContinueReceivingData(bytesNeeded, msgBlob, e);
							}
						}
					}
				}
			}
		}

		private void HandleRawReceive(CommToken token, int bytesTransferred, SocketAsyncEventArgs e)
		{
			if (bytesTransferred == 0)
			{
				handler.EndOfStream();
			}
			else
			{
				bytesNeeded = token.tactics.RawDataInterpreter != null ? token.tactics.RawDataInterpreter.OnDataArrived(token.ownerChannel, token.blob, bytesTransferred) : 0;
				if (bytesNeeded == 0)
				{
					bytesNeeded = handler.RawPoolSize;
					token.blob.Release();
					handler.ContinueReceivingData(bytesNeeded, handler.GetBlobFromPool(bytesNeeded), e);
					if (token.tactics.RawDataInterpreter is IContinuable ac)
					{
						ac.Continue();
					}

				}
				else
				{
					handler.ContinueReceivingData(bytesNeeded, token.blob, e);
				}
			}
		}
	}
}
