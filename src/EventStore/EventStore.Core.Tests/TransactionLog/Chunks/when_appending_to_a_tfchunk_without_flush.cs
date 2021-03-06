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
using System.IO;
using EventStore.Core.TransactionLog;
using EventStore.Core.TransactionLog.LogRecords;
using NUnit.Framework;

namespace EventStore.Core.Tests.TransactionLog.Chunks
{
    [TestFixture]
    public class when_appending_to_a_tfchunk_without_flush
    {
        readonly string _filename = Path.Combine(Path.GetTempPath(), "foo");
        private Core.TransactionLog.Chunks.TFChunk _chunk;
        private Guid _corrId = Guid.NewGuid();
        private Guid _eventId = Guid.NewGuid();
        private RecordWriteResult _result;
        private PrepareLogRecord _record;

        [SetUp]
        public void Setup()
        {
            _record = new PrepareLogRecord(0, _corrId, _eventId, 0, 0, "test", 1, new DateTime(2000, 1, 1, 12, 0, 0),
                                           PrepareFlags.None, "Foo", new byte[12], new byte[15]);
            _chunk = Core.TransactionLog.Chunks.TFChunk.CreateNew(_filename, 4096, 0, 0);
            _result = _chunk.TryAppend(_record);
        }

        [Test]
        public void the_record_is_appended()
        {
            Assert.IsTrue(_result.Success);
        }

        [Test]
        public void the_old_position_is_returned()
        {
            //position without header.
            Assert.AreEqual(0, _result.OldPosition);
        }

        [Test]
        public void the_updated_position_is_returned()
        {
            //position without header.
            Assert.AreEqual(_record.GetSizeWithLengthPrefixAndSuffix(), _result.NewPosition);
        }

        [TearDown]
        public void TearDown()
        {
            _chunk.Dispose();
            File.Delete(_filename);
        }
    }
}