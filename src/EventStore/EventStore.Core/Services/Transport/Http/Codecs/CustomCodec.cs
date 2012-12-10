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
using EventStore.Common.Utils;
using EventStore.Transport.Http;

namespace EventStore.Core.Services.Transport.Http.Codecs
{
    public class CustomCodec : ICodec
    {
        public ICodec BaseCodec { get { return _codec; } }
        public string ContentType { get { return _contentType; } }

        private readonly ICodec _codec;
        private readonly string _contentType;
        private readonly string _type;
        private readonly string _subtype;

        internal CustomCodec(ICodec codec, string contentType)
        {
            Ensure.NotNull(codec, "codec");
            Ensure.NotNull(contentType, "contentType");

            _codec = codec;
            _contentType = contentType;
            var parts = contentType.Split(new[] {'/'}, 2);
            if (parts.Length != 2)
                throw new ArgumentException("contentType");
            _type = parts[0];
            _subtype = parts[1];
        }

        public bool CanParse(string format)
        {
            return string.Equals(format, _contentType, StringComparison.OrdinalIgnoreCase);
        }

        public bool SuitableForReponse(AcceptComponent component)
        {
            return component.MediaType == "*"
                   || (string.Equals(component.MediaType, _type, StringComparison.OrdinalIgnoreCase)
                       && (component.MediaSubtype == "*"
                           || string.Equals(component.MediaSubtype, _subtype, StringComparison.OrdinalIgnoreCase)));
        }

        public T From<T>(string text)
        {
            return _codec.From<T>(text);
        }

        public string To<T>(T value)
        {
            return _codec.To(value);
        }

        public string BeginChunked()
        {
            throw new NotImplementedException();
        }

        public string ChunkSeparator()
        {
            throw new NotImplementedException();
        }

        public string EndChunk()
        {
            throw new NotImplementedException();
        }
    }
}