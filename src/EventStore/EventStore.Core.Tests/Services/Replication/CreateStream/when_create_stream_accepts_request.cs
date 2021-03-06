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

using System.Collections.Generic;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.RequestManager.Managers;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Tests.Fakes;
using NUnit.Framework;

namespace EventStore.Core.Tests.Services.Replication.CreateStream
{
    public class when_create_stream_accepts_request : RequestManagerSpecification
    {
        protected override TwoPhaseRequestManagerBase OnManager(FakePublisher publisher)
        {
            return new CreateStreamTwoPhaseRequestManager(publisher, 3,3);
        }

        protected override IEnumerable<Message> WithInitialMessages()
        {
            yield break;
        }

        protected override Message When()
        {
            return new StorageMessage.CreateStreamRequestCreated(CorrelationId, new NoopEnvelope(), "test123", false, Metadata);
        }

        [Test]
        public void two_messages_are_created()
        {
            Assert.AreEqual(2, produced.Count);
        }

        [Test]
        public void the_first_message_is_write_prepare_with_correct_info()
        {
            Assert.IsInstanceOf<StorageMessage.WritePrepares>(produced[0]);
            var msg = (StorageMessage.WritePrepares) produced[0];
            Assert.AreEqual(CorrelationId, msg.CorrelationId);
            Assert.AreEqual("test123", msg.EventStreamId);
        }

        [Test]
        public void the_second_message_is_timer_schedule()
        {
            Assert.IsInstanceOf<TimerMessage.Schedule>(produced[1]);
            var msg = (TimerMessage.Schedule) produced[1];
            var reply = (StorageMessage.PreparePhaseTimeout) msg.ReplyMessage;
            Assert.AreEqual(Timeouts.PrepareTimeout, msg.TriggerAfter);
            Assert.AreEqual(CorrelationId, reply.CorrelationId);
        }
    }
}