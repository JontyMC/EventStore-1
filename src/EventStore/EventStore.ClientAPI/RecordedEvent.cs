﻿// Copyright (c) 2012, Event Store LLP
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
using EventStore.ClientAPI.Messages;

namespace EventStore.ClientAPI
{
    public class RecordedEvent
    {
        public readonly string EventStreamId;

        public readonly Guid EventId;
        public readonly int EventNumber;

        public readonly string EventType;

        public readonly byte[] Data;
        public readonly byte[] Metadata;

        internal RecordedEvent(ClientMessage.EventRecord systemRecord)
        {
            EventStreamId = systemRecord.EventStreamId;

            EventId = new Guid(systemRecord.EventId);
            EventNumber = systemRecord.EventNumber;

            EventType = systemRecord.EventType;

            Data = systemRecord.Data;
            Metadata = systemRecord.Metadata;
        }

        internal RecordedEvent(ClientMessage.StreamEventAppeared streamEvent)
        {
            EventStreamId = streamEvent.EventStreamId;

            EventId = new Guid(streamEvent.EventId);
            EventNumber = streamEvent.EventNumber;

            EventType = streamEvent.EventType;

            Data = streamEvent.Data;
            Metadata = streamEvent.Metadata;
        }
    }
}
