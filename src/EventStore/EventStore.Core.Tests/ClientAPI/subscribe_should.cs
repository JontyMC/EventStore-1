// Copyright (c) 2012, Event Store LLP
// All rights reserved.
//  
// Redistribution and use in source and binary forms, with or without
// modification, are permitted provided that the following conditions are
// met:
//  
// Redistributions of source code must retain the above copyright notice,
// this list of conditions and the following disclaimer.
// Redistributions in binary form must reproduce the above copyright
// notice, this list of conditions and the following disclaimer in the
// documentation and/or other materials provided with the distribution.
// Neither the name of the Event Store LLP nor the names of its
// contributors may be used to endorse or promote products derived from
// this software without specific prior written permission
// THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS
// "AS IS" AND ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT
// LIMITED TO, THE IMPLIED WARRANTIES OF MERCHANTABILITY AND FITNESS FOR
// A PARTICULAR PURPOSE ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
// HOLDER OR CONTRIBUTORS BE LIABLE FOR ANY DIRECT, INDIRECT, INCIDENTAL,
// SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES (INCLUDING, BUT NOT
// LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES; LOSS OF USE,
// DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND ON ANY
// THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
// (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE
// OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
//  
using System;
using System.Threading;
using EventStore.ClientAPI;
using NUnit.Framework;

namespace EventStore.Core.Tests.ClientAPI
{
    [TestFixture, Category("LongRunning"), Explicit]
    internal class subscribe_should
    {
        public int Timeout = 1000;

        [Test]
        public void be_able_to_subscribe_to_non_existing_stream_and_then_catch_created_event()
        {
            const string stream = "subscribe_should_be_able_to_subscribe_to_non_existing_stream_and_then_catch_created_event";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(MiniNode.Instance.TcpEndPoint);
                var appeared = new CountdownEvent(1);
                var dropped = new CountdownEvent(1);

                Action<RecordedEvent, Position> eventAppeared = (x, p) => appeared.Signal();
                Action subscriptionDropped = () => dropped.Signal();

                store.SubscribeAsync(stream, eventAppeared, subscriptionDropped);

                var create = store.CreateStreamAsync(stream, false, new byte[0]);
                Assert.That(create.Wait(Timeout));

                Assert.That(appeared.Wait(Timeout));
            }
        }

        [Test]
        public void allow_multiple_subscriptions_to_same_stream()
        {
            const string stream = "subscribe_should_allow_multiple_subscriptions_to_same_stream";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(MiniNode.Instance.TcpEndPoint);
                var appeared = new CountdownEvent(2);
                var dropped = new CountdownEvent(2);

                Action<RecordedEvent, Position> eventAppeared = (x, p) => appeared.Signal();
                Action subscriptionDropped = () => dropped.Signal();

                store.SubscribeAsync(stream, eventAppeared, subscriptionDropped);
                store.SubscribeAsync(stream, eventAppeared, subscriptionDropped);

                var create = store.CreateStreamAsync(stream, false, new byte[0]);
                Assert.That(create.Wait(Timeout));

                Assert.That(appeared.Wait(Timeout));
            }
        }

        [Test]
        public void call_dropped_callback_after_unsubscribe_method_call()
        {
            const string stream = "subscribe_should_call_dropped_callback_after_unsubscribe_method_call";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(MiniNode.Instance.TcpEndPoint);
                var appeared =  new CountdownEvent(1);
                var dropped = new CountdownEvent(1);

                Action<RecordedEvent, Position> eventAppeared = (x, p) => appeared.Signal();
                Action subscriptionDropped = () => dropped.Signal();

                store.SubscribeAsync(stream, eventAppeared, subscriptionDropped);
                Assert.That(!appeared.Wait(50));

                store.UnsubscribeAsync(stream);
                Assert.That(dropped.Wait(Timeout));
            }
        }

        [Test]
        public void subscribe_to_deleted_stream_as_well_but_never_invoke_user_callbacks()
        {
            const string stream = "subscribe_should_subscribe_to_deleted_stream_as_well_but_never_invoke_user_callbacks";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(MiniNode.Instance.TcpEndPoint);
                var appeared = new CountdownEvent(1);
                var dropped = new CountdownEvent(1);

                Action<RecordedEvent, Position> eventAppeared = (x, p) => appeared.Signal();
                Action subscriptionDropped = () => dropped.Signal();

                var create = store.CreateStreamAsync(stream, false, new byte[0]);
                Assert.That(create.Wait(Timeout));
                var delete = store.DeleteStreamAsync(stream, ExpectedVersion.EmptyStream);
                Assert.That(delete.Wait(Timeout));

                var subscribe = store.SubscribeAsync(stream, eventAppeared, subscriptionDropped);
                Assert.That(!subscribe.Wait(50));
                Assert.That(!dropped.Wait(50));
            }
        }

        [Test]
        public void not_call_dropped_if_stream_was_deleted()
        {
            const string stream = "subscribe_should_not_call_dropped_if_stream_was_deleted";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(MiniNode.Instance.TcpEndPoint);
                var appeared = new CountdownEvent(1);
                var dropped = new CountdownEvent(1);

                Action<RecordedEvent, Position> eventAppeared = (x, p) => appeared.Signal();
                Action subscriptionDropped = () => dropped.Signal();

                var create = store.CreateStreamAsync(stream, false, new byte[0]);
                Assert.That(create.Wait(Timeout));

                var subscribe = store.SubscribeAsync(stream, eventAppeared, subscriptionDropped);

                var delete = store.DeleteStreamAsync(stream, ExpectedVersion.EmptyStream);
                Assert.That(delete.Wait(Timeout));

                Assert.That(appeared.Wait(Timeout));

                Assert.That(!dropped.Wait(50));
                Assert.That(!subscribe.Wait(50));
            }
        }

        [Test]
        public void catch_created_and_deleted_events_as_well()
        {
            const string stream = "subscribe_should_catch_created_and_deleted_events_as_well";
            using (var store = EventStoreConnection.Create())
            {
                store.Connect(MiniNode.Instance.TcpEndPoint);
                var appeared = new CountdownEvent(2);
                var dropped = new CountdownEvent(1);

                Action<RecordedEvent, Position> eventAppeared = (x, p) => appeared.Signal();
                Action subscriptionDropped = () => dropped.Signal();

                store.SubscribeAsync(stream, eventAppeared, subscriptionDropped);

                var create = store.CreateStreamAsync(stream, false, new byte[0]);
                Assert.That(create.Wait(Timeout));
                var delete = store.DeleteStreamAsync(stream, ExpectedVersion.EmptyStream);
                Assert.That(delete.Wait(Timeout));

                Assert.That(appeared.Wait(Timeout));
            }
        }
    }
}
