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
using System.Collections.Generic;
using EventStore.Core.Bus;
using EventStore.Core.Messaging;

namespace EventStore.Projections.Core.Services
{
    public class RequestResponseSessionDispatcher<TRequest, TResponse, TResponseBegin, TResponsePart, TResponseEnd> : IHandle<TResponse>
        where TRequest : Message 
        where TResponse: Message
        where TResponseBegin : TResponse
        where TResponsePart : TResponse
        where TResponseEnd : TResponse
    {
        private class Item
        {
            private readonly Action<TResponseBegin> _onBegin;
            private readonly Action<TResponsePart> _onPart;
            private readonly Action<TResponseEnd> _onEnd;

            public Item(Action<TResponseBegin> onBegin, Action<TResponsePart> onPart, Action<TResponseEnd> onEnd)
            {
                _onBegin = onBegin;
                _onPart = onPart;
                _onEnd = onEnd;
            }

            public Action<TResponseBegin> OnBegin
            {
                get { return _onBegin; }
            }

            public Action<TResponsePart> OnPart
            {
                get { return _onPart; }
            }

            public Action<TResponseEnd> OnEnd
            {
                get { return _onEnd; }
            }
        }

        //NOTE: this class is not intened to be used from multiple threads, 
        //however we support count requests from other threads for statistics purposes
        private readonly Dictionary<Guid, Item> _map = new Dictionary<Guid, Item>();
        private readonly IPublisher _publisher;
        private readonly Func<TRequest, Guid> _getRequestCorrelationId;
        private readonly Func<TResponse, Guid> _getResponseCorrelationId;
        private readonly IEnvelope _defaultReplyEnvelope;

        public RequestResponseSessionDispatcher(
            IPublisher publisher, Func<TRequest, Guid> getRequestCorrelationId,
            Func<TResponse, Guid> getResponseCorrelationId, IEnvelope defaultReplyEnvelope)
        {
            _publisher = publisher;
            _getRequestCorrelationId = getRequestCorrelationId;
            _getResponseCorrelationId = getResponseCorrelationId;
            _defaultReplyEnvelope = defaultReplyEnvelope;
        }

        public Guid Publish(TRequest request, Action<TResponseBegin> onBegin, Action<TResponsePart> onPart, Action<TResponseEnd> onEnd)
        {
            //TODO: expiration?
            Guid requestCorrelationId;
            lock (_map)
            {
                requestCorrelationId = _getRequestCorrelationId(request);
                _map.Add(requestCorrelationId, new Item(onBegin, onPart, onEnd));
            }
            _publisher.Publish(request);
            //NOTE: the following condition is required as publishing the message could also process the message 
            // and the correlationId is already invalid here
            return _map.ContainsKey(requestCorrelationId) ? requestCorrelationId : Guid.Empty;
        }

        void Handle(TResponseBegin message)
        {
            var correlationId = _getResponseCorrelationId(message);
            Item action;
            lock (_map)
                if (_map.TryGetValue(correlationId, out action))
                {
                    action.OnBegin(message);
                }
        }

        void Handle(TResponsePart message)
        {
            var correlationId = _getResponseCorrelationId(message);
            Item action;
            lock (_map)
                if (_map.TryGetValue(correlationId, out action))
                {
                    action.OnPart(message);
                }
        }

        void Handle(TResponseEnd message)
        {
            var correlationId = _getResponseCorrelationId(message);
            Item action;
            lock (_map)
                if (_map.TryGetValue(correlationId, out action))
                {
                    _map.Remove(correlationId);
                    action.OnEnd(message);
                }
        }

        public IEnvelope Envelope
        {
            get { return _defaultReplyEnvelope; }
        }

        public void Cancel(Guid requestId)
        {
            lock (_map)
                _map.Remove(requestId);
        }

        public void CancelAll()
        {
            lock (_map)
                _map.Clear();
        }

        public void Handle(TResponse message)
        {
            var part = message as TResponsePart;
            if (part != null)
                Handle(part);
            else
            {
                var begin = message as TResponseBegin;
                if (begin != null)
                    Handle(begin);
                else
                {
                    var end = message as TResponseEnd;
                    if (end != null)
                        Handle(end);
                }
            }
        }
    }
}
