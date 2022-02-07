using Detekonai.Core;
using Detekonai.Networking.Runtime.AsyncEvent;
using Detekonai.Networking.Runtime.Raw;
using Detekonai.Networking.Runtime.Strategy;
using Detekonai.Networking.Runtime.Tcp;
using NSubstitute;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Detekonai.Networking.Tests.Runtime.Tcp
{
    class TcpPacketBuilderTest
    {
        private BinaryBlobPool pool;
        private CommToken token = new CommToken();
        private SocketAsyncEventArgs args = new SocketAsyncEventArgs();
        
        public TcpPacketBuilderTest()
        {
            args.UserToken = token;
        }

        [SetUp]
        public void SetUp() 
        {
            pool = new BinaryBlobPool(5, 64);
        }

        [Test]
        public void Recieve_a_header_only_package()
        {
            var handler = Substitute.For<TcpPacketBuilder.ITcpPacketHandler>();
            TcpPacketBuilder builder = new TcpPacketBuilder(handler);

            token.blob = pool.GetBlob();
            TcpPacketBuilder.AddHeader(token.blob, CommToken.HeaderFlags.None, 0, 1);

            bool arrived = false;
            handler.When(x => x.CommReceived(Arg.Any<CommToken>())).Do(x => arrived = true);

            builder.Receive(token, 6, args);

      
            Assert.That(arrived, Is.True, "We shoud get the complete message");
            Assert.That(pool.AvailableChunks, Is.EqualTo(5), "All blobs must be released at the end");
        }

        [Test]
        public void Recieve_a_package_bigger_than_default_pool()
        {
            string msg = "Hey there is a message";
            int msgSize = msg.Length + 4;
            BinaryBlobPool smallPool = new BinaryBlobPool(2, 10);

            var handler = Substitute.For<TcpPacketBuilder.ITcpPacketHandler>();

            TcpPacketBuilder builder = new TcpPacketBuilder(handler);

            token.blob = smallPool.GetBlob();
            TcpPacketBuilder.AddHeader(token.blob, CommToken.HeaderFlags.None, (uint)msgSize, 1);
            string finalValue = "";
            handler.GetBlobFromPool(Arg.Any<int>()).Returns(x => pool.GetBlob());
            handler.When(x => x.CommReceived(Arg.Any<CommToken>())).Do(x => finalValue = x.Arg<CommToken>().blob.ReadString());
            handler.WhenForAnyArgs(x => x.ContinueReceivingData(default, default, default)).Do(Callback.First(x => {
                token.blob = x.Arg<BinaryBlob>();
                token.blob.AddString(msg);
                builder.Receive(token, msgSize, args);
            }));

            builder.Receive(token, 6, args);
            
            Assert.That(finalValue, Is.EqualTo(msg), "We shoud get the complete message");
            Assert.That(pool.AvailableChunks, Is.EqualTo(4), "All blobs except the active one must be released at the end");
            Assert.That(smallPool.AvailableChunks, Is.EqualTo(2), "Original blob should be released");
        }

        [Test]
        public void Recieve_a_package_bigger_than_all_pool()
        {
            string msg = "Hey there is a message";
            int msgSize = msg.Length + 4;
            BinaryBlobPool smallPool = new BinaryBlobPool(2, 10);

            var handler = Substitute.For<TcpPacketBuilder.ITcpPacketHandler>();

            TcpPacketBuilder builder = new TcpPacketBuilder(handler);

            token.blob = smallPool.GetBlob();
            TcpPacketBuilder.AddHeader(token.blob, CommToken.HeaderFlags.None, (uint)msgSize, 1);
            handler.GetBlobFromPool(Arg.Any<int>()).Returns(x => throw new InvalidOperationException());
           
            handler.WhenForAnyArgs(x => x.ContinueReceivingData(default, default, default)).Do(Callback.First(x => {
                token.blob = x.Arg<BinaryBlob>();
                token.blob.AddString(msg);
                builder.Receive(token, msgSize, args);
            }));

            
            Assert.Throws<InvalidOperationException>( () => builder.Receive(token, 6, args), "If there is no suitable blob pool we throw an exception");
            handler.DidNotReceiveWithAnyArgs().CommReceived(default);
            Assert.That(pool.AvailableChunks, Is.EqualTo(5), "All blobs must be released at the end");
            Assert.That(smallPool.AvailableChunks, Is.EqualTo(2), "Original blob should be released");
        }

        [Test]
        public void Recieve_a_simple_package()
        {
            string msg = "Hey there is a message";
            int msgSize = msg.Length + 4;

            var handler = Substitute.For<TcpPacketBuilder.ITcpPacketHandler>();
            TcpPacketBuilder builder = new TcpPacketBuilder(handler);

            token.blob = pool.GetBlob();
            TcpPacketBuilder.AddHeader(token.blob, CommToken.HeaderFlags.None, (uint)msgSize, 1);
            string finalValue = "";
            handler.When(x => x.CommReceived(Arg.Any<CommToken>())).Do(x => finalValue = x.Arg<CommToken>().blob.ReadString());
            handler.WhenForAnyArgs(x => x.ContinueReceivingData(default, default, default)).Do(Callback.First(x => {
                token.blob = x.Arg<BinaryBlob>();
                token.blob.AddString(msg);
                builder.Receive(token, msgSize, args);
            }));

            builder.Receive(token, 6, args);

            Assert.That(finalValue, Is.EqualTo(msg), "We shoud get the complete message");
            Assert.That(pool.AvailableChunks, Is.EqualTo(5), "All blobs must be released at the end");
        }

        [Test]
        public void Recieve_a_package_we_handle_with_TAP()
        {
            string msg = "Hey there is a message";
            int msgSize = msg.Length + 4;
            string msg2 = "Another message";
            int msg2Size = msg.Length + 4;
            var handler = Substitute.For<TcpPacketBuilder.ITcpPacketHandler>();
            TcpPacketBuilder builder = new TcpPacketBuilder(handler);

            token.blob = pool.GetBlob();
            TcpPacketBuilder.AddHeader(token.blob, CommToken.HeaderFlags.None, (uint)msgSize, 1);
            string finalValue2 = "";
            BinaryBlob tapBlob = null;
            handler.GetBlobFromPool(Arg.Any<int>()).Returns(x => pool.GetBlob());
            handler.When(x => x.CommReceived(Arg.Any<CommToken>())).Do(Callback
                .First(x => {
                    tapBlob = x.Arg<CommToken>().blob;
                    x.Arg<CommToken>().blob = null; //keep the blob for later(eg for TAP)
                }).Then(x => {
                    finalValue2 = x.Arg<CommToken>().blob.ReadString();
                })
                );
            handler.WhenForAnyArgs(x => x.ContinueReceivingData(default, default, default)).Do(Callback
                .First(x => {
                    token.blob = x.Arg<BinaryBlob>();
                    token.blob.AddString(msg);
                    builder.Receive(token, msgSize, args);
                }).Then(x => {
                    token.blob = x.Arg<BinaryBlob>();
                    TcpPacketBuilder.AddHeader(token.blob, CommToken.HeaderFlags.None, (uint)msgSize, 2);
                    builder.Receive(token, 6, args);
                }).Then(x => {
                    token.blob = x.Arg<BinaryBlob>();
                    token.blob.AddString(msg2);
                    builder.Receive(token, msg2Size, args);
                })
            );

            builder.Receive(token, 6, args);

            Assert.That(tapBlob, Is.Not.Null, "Blob selected for TAP should not be null");
            Assert.That(tapBlob.InUse, Is.True, "Blob selected for TAP should not be released");
            Assert.That(tapBlob.ReadString(), Is.EqualTo(msg), "We shoud get the complete message");
            Assert.That(finalValue2, Is.EqualTo(msg2), "We shoud get the complete message");
            Assert.That(pool.AvailableChunks, Is.EqualTo(3), "All blobs except the active one and the TAP one must be released at the end");
        }

        [Test]
        public void Recieve_a_multi_part_package()
        {
            string msg = "Hey there ";
            int msgSize = msg.Length + 4;
            string msg2 = "is a message";
            int msg2Size = msg2.Length + 4;

            var handler = Substitute.For<TcpPacketBuilder.ITcpPacketHandler>();
            TcpPacketBuilder builder = new TcpPacketBuilder(handler);

            token.blob = pool.GetBlob();
            TcpPacketBuilder.AddHeader(token.blob, CommToken.HeaderFlags.None, (uint)msgSize+(uint)msg2Size, 1);
            string finalValue = "";
            handler.When(x => x.CommReceived(Arg.Any<CommToken>())).Do(x => finalValue = x.Arg<CommToken>().blob.ReadString());
            
            handler.WhenForAnyArgs(x => x.ContinueReceivingData(default, default, default)).Do(Callback.First(x => {
                token.blob = x.Arg<BinaryBlob>();
                token.blob.AddString(msg);
                builder.Receive(token, msgSize, args);
            }).Then(x => {
                token.blob = x.Arg<BinaryBlob>();
                token.blob.AddString(msg2);
                builder.Receive(token, msg2Size, args);
            })
            
            );

            builder.Receive(token, 6, args);

            Assert.That(finalValue, Is.EqualTo(msg), "We shoud get the complete message");
            Assert.That(pool.AvailableChunks, Is.EqualTo(5), "All blobs nust be released at the end");
        }

        [Test]
        public void Recieve_a_tight_package()
        {
            string msg = "Hey there is a message";
            int msgSize = msg.Length + 4;
            BinaryBlobPool smallPool = new BinaryBlobPool(2, msgSize);
            var handler = Substitute.For<TcpPacketBuilder.ITcpPacketHandler>();
            TcpPacketBuilder builder = new TcpPacketBuilder(handler);
            token.blob = smallPool.GetBlob();
            TcpPacketBuilder.AddHeader(token.blob, CommToken.HeaderFlags.None, (uint)msgSize, 1);
            string finalValue = "";
            handler.When(x => x.CommReceived(Arg.Any<CommToken>())).Do(x => finalValue = x.Arg<CommToken>().blob.ReadString());
            handler.WhenForAnyArgs(x => x.ContinueReceivingData(default, default, default)).Do(Callback.First(x => {
                token.blob = x.Arg<BinaryBlob>();
                token.blob.AddString(msg);
                builder.Receive(token, msgSize, args);
            }));

            builder.Receive(token, 6, args);

            Assert.That(finalValue, Is.EqualTo(msg), "We shoud get the complete message");
            Assert.That(pool.AvailableChunks, Is.EqualTo(5), "All blobs nust be released at the end");

        }

        [Test]
        public void Recieve_a_raw_package_without_interpreter()
        {
            string msg = "Hey there is a message";
            int msgSize = msg.Length + 4;
            var handler = Substitute.For<TcpPacketBuilder.ITcpPacketHandler>();
            var tactics = Substitute.For<ICommTactics>();
            tactics.RawDataInterpreter.Returns((IRawCommInterpreter)null);
            token.tactics = tactics;
            TcpPacketBuilder builder = new TcpPacketBuilder(handler);
            builder.Mode = ICommChannel.EChannelMode.Raw;
            token.blob = pool.GetBlob();
            token.blob.AddString(msg);
            handler.WhenForAnyArgs(x => x.ContinueReceivingData(default, default, default)).Do(Callback.First(x => {
                token.blob = x.Arg<BinaryBlob>();
            }));

            builder.Receive(token, msgSize, args);

            handler.DidNotReceiveWithAnyArgs().CommReceived(default);
            Assert.That(pool.AvailableChunks, Is.EqualTo(5), "All blobs nust be released at the end");
        }

        [Test]
        public void Recieve_a_simple_raw_package()
        {
            string msg = "Hey there is a message";
            var handler = Substitute.For<TcpPacketBuilder.ITcpPacketHandler>();
            var tactics = Substitute.For<ICommTactics>();
            var interpreter = Substitute.For<IRawCommInterpreter>();

            tactics.RawDataInterpreter.Returns(interpreter);
            token.tactics = tactics;
            TcpPacketBuilder builder = new TcpPacketBuilder(handler);
            builder.Mode = ICommChannel.EChannelMode.Raw;
            token.blob = pool.GetBlob();
            token.blob.AddString(msg);

            string finalValue = "";
            interpreter.OnDataArrived(Arg.Any<ICommChannel>(),Arg.Any<BinaryBlob>(), Arg.Any<int>()).Returns(x => {
                x.Arg<BinaryBlob>().JumpIndexToBegin();
                finalValue = x.Arg<BinaryBlob>().ReadString();
                return 0;
            });

            handler.WhenForAnyArgs(x => x.ContinueReceivingData(default, default, default)).Do(Callback.First(x => {
                token.blob = x.Arg<BinaryBlob>();
            }));

            builder.Receive(token, msg.Length + 4, args);

            handler.DidNotReceiveWithAnyArgs().CommReceived(default);
            Assert.That(finalValue, Is.EqualTo(msg), "We shoud get the complete message");
            Assert.That(pool.AvailableChunks, Is.EqualTo(5), "All blobs nust be released at the end");
        }

        [Test]
        public void Recieve_a_multi_read_raw_package()
        {
            string msg = "Hey there is";
            string msg2 = " a message";
            var handler = Substitute.For<TcpPacketBuilder.ITcpPacketHandler>();
            var tactics = Substitute.For<ICommTactics>();
            var interpreter = Substitute.For<IRawCommInterpreter>();

            tactics.RawDataInterpreter.Returns(interpreter);
            token.tactics = tactics;
            TcpPacketBuilder builder = new TcpPacketBuilder(handler);
            builder.Mode = ICommChannel.EChannelMode.Raw;
            token.blob = pool.GetBlob();
            token.blob.AddString(msg);

            string finalValue = "";
            interpreter.OnDataArrived(Arg.Any<ICommChannel>(), Arg.Any<BinaryBlob>(), Arg.Any<int>()).Returns(x => {
                return msg2.Length;
            },x => {
                x.Arg<BinaryBlob>().JumpIndexToBegin();
                finalValue = x.Arg<BinaryBlob>().ReadString()+ x.Arg<BinaryBlob>().ReadString();
                return 0;
            });

            handler.WhenForAnyArgs(x => x.ContinueReceivingData(default, default, default)).Do(Callback.First(x => {
                token.blob = x.Arg<BinaryBlob>();
                token.blob.AddString(msg2);
                builder.Receive(token, msg2.Length+4, args);
            }));

            builder.Receive(token, msg.Length+4, args);

            handler.DidNotReceiveWithAnyArgs().CommReceived(default);
            Assert.That(finalValue, Is.EqualTo(msg+msg2), "We shoud get the complete message");
            Assert.That(pool.AvailableChunks, Is.EqualTo(5), "All blobs nust be released at the end");
        }


        [Test]
        public void First_receive_after_managed_transition_only_header()
        {
            string msg = "Hey there";
            int msgSize = msg.Length + 4;

            var handler = Substitute.For<TcpPacketBuilder.ITcpPacketHandler>();
            TcpPacketBuilder builder = new TcpPacketBuilder(handler);
            builder.Mode = ICommChannel.EChannelMode.Raw;
            builder.Mode = ICommChannel.EChannelMode.Managed;

            token.blob = pool.GetBlob();
            TcpPacketBuilder.AddHeader(token.blob, CommToken.HeaderFlags.None, (uint)msgSize, 1);

            string finalValue = "";
            handler.GetBlobFromPool(Arg.Any<int>()).Returns(x => pool.GetBlob());
            handler.When(x => x.CommReceived(Arg.Any<CommToken>())).Do(x => finalValue = x.Arg<CommToken>().blob.ReadString());
            handler.WhenForAnyArgs(x => x.ContinueReceivingData(default, default, default)).Do(Callback.First(x => {
                token.blob = x.Arg<BinaryBlob>();
                token.blob.AddString(msg);
                builder.Receive(token, msgSize, args);
            })

            );

            builder.Receive(token, 6, args);

            Assert.That(finalValue, Is.EqualTo(msg), "We shoud get the complete message");
            Assert.That(pool.AvailableChunks, Is.EqualTo(4), "All blobs except the active one must be released at the end");
        }

        [Test]
        public void First_receive_after_managed_transition_one_complete_packet()
        {
            string msg = "Hey there";
            int msgSize = msg.Length + 4;

            var handler = Substitute.For<TcpPacketBuilder.ITcpPacketHandler>();
            TcpPacketBuilder builder = new TcpPacketBuilder(handler);
            builder.Mode = ICommChannel.EChannelMode.Raw;
            builder.Mode = ICommChannel.EChannelMode.Managed;

            token.blob = pool.GetBlob();
            TcpPacketBuilder.AddHeader(token.blob, CommToken.HeaderFlags.None, (uint)msgSize, 1);
            token.blob.AddString(msg);
            string finalValue = "";
            handler.GetBlobFromPool(Arg.Any<int>()).Returns(x => pool.GetBlob());
            handler.When(x => x.CommReceived(Arg.Any<CommToken>())).Do(x => finalValue = x.Arg<CommToken>().blob.ReadString());
            handler.WhenForAnyArgs(x => x.ContinueReceivingData(default, default, default)).Do(x =>  token.blob = x.Arg<BinaryBlob>());

            builder.Receive(token, 6+msgSize, args);

            Assert.That(finalValue, Is.EqualTo(msg), "We shoud get the complete message");
            Assert.That(pool.AvailableChunks, Is.EqualTo(4), "All blobs except the active one must be released at the end");
        }

        [Test]
        public void First_receive_after_managed_transition_two_complete_packets()
        {
            string msg = "Hey there";
            int msgSize = msg.Length + 4;
            string msg2 = "A longer than first message";
            int msg2Size = msg2.Length + 4;

            var handler = Substitute.For<TcpPacketBuilder.ITcpPacketHandler>();
            TcpPacketBuilder builder = new TcpPacketBuilder(handler);
            builder.Mode = ICommChannel.EChannelMode.Raw;
            builder.Mode = ICommChannel.EChannelMode.Managed;

            token.blob = pool.GetBlob();
            TcpPacketBuilder.AddHeader(token.blob, CommToken.HeaderFlags.None, (uint)msgSize, 1);
            token.blob.AddString(msg);
            TcpPacketBuilder.AddHeader(token.blob, CommToken.HeaderFlags.None, (uint)msg2Size, 2);
            token.blob.AddString(msg2);
            string msg1Value = "";
            string msg2Value = "";
            handler.GetBlobFromPool(Arg.Any<int>()).Returns(x => pool.GetBlob());
            handler.When(x => x.CommReceived(Arg.Any<CommToken>())).Do(Callback
                .First(x => msg1Value = x.Arg<CommToken>().blob.ReadString())
                .Then(x => msg2Value = x.Arg<CommToken>().blob.ReadString()));
            handler.WhenForAnyArgs(x => x.ContinueReceivingData(default, default, default)).Do(x => token.blob = x.Arg<BinaryBlob>());

            builder.Receive(token, 6 + msgSize+6+msg2Size, args);

            Assert.That(msg1Value, Is.EqualTo(msg), "We shoud get the complete message");
            Assert.That(msg2Value, Is.EqualTo(msg2), "We shoud get the complete message");
            Assert.That(pool.AvailableChunks, Is.EqualTo(4), "All blobs except the active one must be released at the end");
        }

        [Test]
        public void First_receive_after_managed_transition_a_complete_packet_and_a_partial_header()
        {
            string msg = "Hey there";
            int msgSize = msg.Length + 4;
            string msg2 = "A longer than first message";
            int msg2Size = msg2.Length + 4;

            var handler = Substitute.For<TcpPacketBuilder.ITcpPacketHandler>();
            TcpPacketBuilder builder = new TcpPacketBuilder(handler);
            builder.Mode = ICommChannel.EChannelMode.Raw;
            builder.Mode = ICommChannel.EChannelMode.Managed;

            token.blob = pool.GetBlob();
            TcpPacketBuilder.AddHeader(token.blob, CommToken.HeaderFlags.None, (uint)msgSize, 1);
            token.blob.AddString(msg);
            token.blob.AddUInt((uint)msg2Size);//partial header

            string msg1Value = "";
            string msg2Value = "";
            handler.GetBlobFromPool(Arg.Any<int>()).Returns(x => pool.GetBlob());
            handler.When(x => x.CommReceived(Arg.Any<CommToken>())).Do(Callback
                .First(x => msg1Value = x.Arg<CommToken>().blob.ReadString())
                .Then(x => msg2Value = x.Arg<CommToken>().blob.ReadString()));
            handler.WhenForAnyArgs(x => x.ContinueReceivingData(default, default, default)).Do(Callback
                .First(x => {
                token.blob = x.Arg<BinaryBlob>();
                token.blob.AddUShort(2); //partial header
                builder.Receive(token, 2, args);
                })
                .Then(x => {
                    token.blob = x.Arg<BinaryBlob>();
                    token.blob.AddString(msg2);
                    builder.Receive(token, msg2Size, args);
                })
                );

            builder.Receive(token, 6 + msgSize + 4, args);

            Assert.That(msg1Value, Is.EqualTo(msg), "We shoud get the complete message");
            Assert.That(msg2Value, Is.EqualTo(msg2), "We shoud get the complete message");
            Assert.That(pool.AvailableChunks, Is.EqualTo(4), "All blobs except the active one must be released at the end");
        }

        [Test]
        public void First_receive_after_managed_transition_a_complete_packet_and_a_complete_header()
        {
            string msg = "Hey there";
            int msgSize = msg.Length + 4;
            string msg2 = "A longer than first message";
            int msg2Size = msg2.Length + 4;

            var handler = Substitute.For<TcpPacketBuilder.ITcpPacketHandler>();
            TcpPacketBuilder builder = new TcpPacketBuilder(handler);
            builder.Mode = ICommChannel.EChannelMode.Raw;
            builder.Mode = ICommChannel.EChannelMode.Managed;

            token.blob = pool.GetBlob();
            TcpPacketBuilder.AddHeader(token.blob, CommToken.HeaderFlags.None, (uint)msgSize, 1);
            token.blob.AddString(msg);
            TcpPacketBuilder.AddHeader(token.blob, CommToken.HeaderFlags.None, (uint)msg2Size, 2);

            string msg1Value = "";
            string msg2Value = "";
            handler.GetBlobFromPool(Arg.Any<int>()).Returns(x => pool.GetBlob());
            handler.When(x => x.CommReceived(Arg.Any<CommToken>())).Do(Callback
                .First(x => msg1Value = x.Arg<CommToken>().blob.ReadString())
                .Then(x => msg2Value = x.Arg<CommToken>().blob.ReadString()));
            handler.WhenForAnyArgs(x => x.ContinueReceivingData(default, default, default)).Do(Callback
                .First(x => {
                    token.blob = x.Arg<BinaryBlob>();
                    token.blob.AddString(msg2);
                    builder.Receive(token, msg2Size, args);
                })
                );

            builder.Receive(token, 6 + msgSize + 6, args);

            Assert.That(msg1Value, Is.EqualTo(msg), "We shoud get the complete message");
            Assert.That(msg2Value, Is.EqualTo(msg2), "We shoud get the complete message");
            Assert.That(pool.AvailableChunks, Is.EqualTo(4), "All blobs except the active one must be released at the end");
        }

        [Test]
        public void First_receive_after_managed_transition_a_complete_packet_and_a_partial_packet()
        {
            string msg = "Hey there";
            int msgSize = msg.Length + 4;
            string msg2 = "A longer than first message";
            int msg2Size = msg2.Length + 4;

            var handler = Substitute.For<TcpPacketBuilder.ITcpPacketHandler>();
            TcpPacketBuilder builder = new TcpPacketBuilder(handler);
            builder.Mode = ICommChannel.EChannelMode.Raw;
            builder.Mode = ICommChannel.EChannelMode.Managed;

            token.blob = pool.GetBlob();
            TcpPacketBuilder.AddHeader(token.blob, CommToken.HeaderFlags.None, (uint)msgSize, 1);
            token.blob.AddString(msg);
            TcpPacketBuilder.AddHeader(token.blob, CommToken.HeaderFlags.None, (uint)msg2Size, 2);
            token.blob.AddString(msg2);
            //remove the last 8 characters
            token.blob.Index -= 8;
            token.blob.AddInt(0);
            token.blob.AddInt(0);
            token.blob.Index -= 8;

            string msg1Value = "";
            string msg2Value = "";
            handler.GetBlobFromPool(Arg.Any<int>()).Returns(x => pool.GetBlob());
            handler.When(x => x.CommReceived(Arg.Any<CommToken>())).Do(Callback
                .First(x => msg1Value = x.Arg<CommToken>().blob.ReadString())
                .Then(x => msg2Value = x.Arg<CommToken>().blob.ReadString()));
            handler.WhenForAnyArgs(x => x.ContinueReceivingData(default, default, default)).Do(Callback
                .First(x => {
                    token.blob = x.Arg<BinaryBlob>();
                    token.blob.AddFixedString(msg2.Substring(msg2.Length-8));
                    builder.Receive(token, 8, args);
                })
                );

            builder.Receive(token, 6 + msgSize + 6+msg2Size-8, args);

            Assert.That(msg1Value, Is.EqualTo(msg), "We shoud get the complete message");
            Assert.That(msg2Value, Is.EqualTo(msg2), "We shoud get the complete message");
            Assert.That(pool.AvailableChunks, Is.EqualTo(4), "All blobs except the active one must be released at the end");
        }

        [Test]
        public void First_receive_after_managed_transition_a_partial_header()
        {
            string msg = "Hey there";
            int msgSize = msg.Length + 4;

            var handler = Substitute.For<TcpPacketBuilder.ITcpPacketHandler>();
            TcpPacketBuilder builder = new TcpPacketBuilder(handler);
            builder.Mode = ICommChannel.EChannelMode.Raw;
            builder.Mode = ICommChannel.EChannelMode.Managed;

            token.blob = pool.GetBlob();
            token.blob.AddUInt((uint)msgSize);//partial header
            string msg1Value = "";
            handler.GetBlobFromPool(Arg.Any<int>()).Returns(x => pool.GetBlob());
            handler.When(x => x.CommReceived(Arg.Any<CommToken>())).Do(x => msg1Value = x.Arg<CommToken>().blob.ReadString());
            handler.WhenForAnyArgs(x => x.ContinueReceivingData(default, default, default)).Do(Callback
                .First(x => {
                    token.blob = x.Arg<BinaryBlob>();
                    token.blob.AddUShort(2); //partial header
                    builder.Receive(token, 2, args);
                })
                .Then(x => {
                    token.blob = x.Arg<BinaryBlob>();
                    token.blob.AddString(msg);
                    builder.Receive(token, msgSize, args);
                })
                );

            builder.Receive(token, 4, args);

            Assert.That(msg1Value, Is.EqualTo(msg), "We shoud get the complete message");
            Assert.That(pool.AvailableChunks, Is.EqualTo(4), "All blobs except the active one must be released at the end");
        }
    }
}
