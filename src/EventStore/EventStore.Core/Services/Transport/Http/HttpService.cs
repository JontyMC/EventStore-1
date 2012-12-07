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
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Net;
using System.Threading;
using EventStore.Common.Log;
using EventStore.Common.Utils;
using EventStore.Core.Bus;
using EventStore.Core.Messages;
using EventStore.Core.Messaging;
using EventStore.Core.Services.TimerService;
using EventStore.Core.Services.Transport.Http.Codecs;
using EventStore.Transport.Http;
using System.Linq;
using EventStore.Transport.Http.EntityManagement;
using EventStore.Transport.Http.Server;
using HttpStatusCode = EventStore.Transport.Http.HttpStatusCode;
using Uri = System.Uri;

namespace EventStore.Core.Services.Transport.Http
{
    public class UriToActionMatch
    {
        public readonly UriTemplateMatch TemplateMatch;
        public readonly ControllerAction ControllerAction;
        public readonly Action<HttpEntity, UriTemplateMatch> RequestHandler;

        public UriToActionMatch(UriTemplateMatch templateMatch, 
                                ControllerAction controllerAction, 
                                Action<HttpEntity, UriTemplateMatch> requestHandler)
        {
            TemplateMatch = templateMatch;
            ControllerAction = controllerAction;
            RequestHandler = requestHandler;
        }
    }

    public enum ServiceAccessibility
    {
        Private,
        Public
    }

    public interface IHttpService
    {
        void RegisterControllerAction(ControllerAction action, Action<HttpEntity, UriTemplateMatch> handler);
    }

    public class HttpService : IHttpService,
                               IHandle<SystemMessage.SystemInit>,
                               IHandle<SystemMessage.BecomeShuttingDown>,
                               IHandle<HttpMessage.SendOverHttp>,
                               IHandle<HttpMessage.UpdatePendingRequests>
    {
        private static readonly TimeSpan UpdateInterval = TimeSpan.FromSeconds(1);
        private static readonly TimeSpan MaxDuration = TimeSpan.FromSeconds(5);

        private readonly ILogger _log = LogManager.GetLoggerFor<HttpService>();

        public bool IsListening
        {
            get
            {
                return _server.IsListening;
            }
        }

        private readonly ServiceAccessibility _accessibility;
        private readonly IPublisher _inputBus;
        private readonly IEnvelope _publishEnvelope;

        private readonly object _pendingLock = new object();
        private readonly SortedSet<HttpEntity> _pending;
        private readonly Dictionary<ControllerAction, Action<HttpEntity, UriTemplateMatch>> _actions;

        private readonly HttpMessagePipe _httpPipe;
        private readonly HttpAsyncServer _server;
        private readonly List<QueuedHandler> _receiveHandlers;
        private long _requestCount = 0;

        public HttpService(ServiceAccessibility accessibility, IPublisher inputBus, string[] prefixes, int receiveHandlerCount)
        {
            Ensure.NotNull(inputBus, "inputBus");
            Ensure.NotNull(prefixes, "prefixes");
            Ensure.Positive(receiveHandlerCount, "receiveHandlerCount");
            _accessibility = accessibility;
            _inputBus = inputBus;
            _publishEnvelope = new PublishEnvelope(inputBus);

            _pending = new SortedSet<HttpEntity>(new HttpEntity.HttpEntityReceivedComparer());
            _actions = new Dictionary<ControllerAction, Action<HttpEntity, UriTemplateMatch>>();

            _httpPipe = new HttpMessagePipe();
            _receiveHandlers = new List<QueuedHandler>();
            for (int i = 0; i < receiveHandlerCount;i++)
            {
                var handler = new QueuedHandler(new NarrowingHandler<Message, IncomingHttpRequestMessage>(new ProcessHttpHandler(this)), "Incoming HTTP " + (i + 1),
                                                true, 50, 5000);
                _receiveHandlers.Add(handler);
                handler.Start();
            }
            _server = new HttpAsyncServer(prefixes);
            _server.RequestReceived += RequestReceived;
        }

        public void Handle(SystemMessage.SystemInit message)
        {
            if (_server.TryStart())
            {
                _inputBus.Publish(TimerMessage.Schedule.Create(UpdateInterval,
                                                               _publishEnvelope,
                                                               new HttpMessage.UpdatePendingRequests(_accessibility)));
            }
            else
                Application.Exit(ExitCode.Error, "Http async server failed to start.");
        }

        public void Handle(SystemMessage.BecomeShuttingDown message)
        {

        }

        public void Handle(HttpMessage.SendOverHttp message)
        {
            _httpPipe.Push(message.Message, message.EndPoint);
        }

        public void Handle(HttpMessage.UpdatePendingRequests message)
        {
            if (_accessibility != message.Accessibility)
                return;

            var now = DateTime.UtcNow;
            var garbage = new List<HttpEntity>();

            lock (_pendingLock)
            {
                while (_pending.Count > 0)
                {
                    var request = _pending.Min;
                    if ((now - request.Received).Duration() < MaxDuration)
                        break;

                    garbage.Add(request);
                    _pending.Remove(request);
                }
            }

            foreach (var request in garbage)
            {
                request.Manager.ReplyStatus(
                    HttpStatusCode.RequestTimeout,
                    "Server was unable to handle request in time",
                    e => _log.ErrorException(e, "Error occured while closing timed out connection (http service core)"));
            }

            _inputBus.Publish(TimerMessage.Schedule.Create(UpdateInterval,
                                                           _publishEnvelope,
                                                           new HttpMessage.UpdatePendingRequests(_accessibility)));
        }

        public void Shutdown()
        {
            _server.Shutdown();
        }

        public void SetupController(IController controller)
        {
            Ensure.NotNull(controller, "controller");
            controller.Subscribe(this, _httpPipe);
        }

        public void RegisterControllerAction(ControllerAction action, Action<HttpEntity, UriTemplateMatch> handler)
        {
            Ensure.NotNull(action, "action");
            Ensure.NotNull(handler, "handler");

            if (!_actions.ContainsKey(action))
                _actions[action] = handler;
        }


        private void RequestReceived(HttpAsyncServer sender, HttpListenerContext context)
        {
            Interlocked.Increment(ref _requestCount);
            _receiveHandlers[(int)(_requestCount % _receiveHandlers.Count)].Handle(new IncomingHttpRequestMessage(sender, context));
        }

        private void ProcessRequest(HttpAsyncServer sender, HttpListenerContext context)
        {
            try
            {
                var allMatches = AllMatches(context.Request.Url);
                var allowedMethods = allMatches.Select(m => m.ControllerAction.HttpMethod).ToArray();

                if (allMatches.Count == 0)
                {
                    NotFound(context);
                    return;
                }

                //add options to the list of allowed request methods
                allowedMethods = allowedMethods.Union(new[] { HttpMethod.Options }).ToArray();
                if (context.Request.HttpMethod.Equals(HttpMethod.Options))
                {
                    RespondWithOptions(context, allowedMethods);
                    return;
                }

                var match = allMatches.LastOrDefault(m => m.ControllerAction.HttpMethod == context.Request.HttpMethod);
                if (match == null)
                {
                    MethodNotAllowed(context, allowedMethods);
                    return;
                }

                ICodec requestCodec;
                if (!TrySelectRequestCodec(context.Request.HttpMethod, 
                                           context.Request.ContentType,
                                           match.ControllerAction.SupportedRequestCodecs, 
                                           out requestCodec))
                {
                    BadCodec(context, "Content-Type MUST be set for POST PUT and DELETE");
                    return;
                }
                ICodec responseCodec;
                if (!TrySelectResponseCodec(context.Request.QueryString, 
                                            context.Request.AcceptTypes,
                                            match.ControllerAction.SupportedResponseCodecs, 
                                            match.ControllerAction.DefaultResponseCodec,
                                            out responseCodec))
                {
                    BadCodec(context, "Requested uri is not available in requested format");
                    return;
                }

                var entity = CreateEntity(DateTime.UtcNow,
                                          context,
                                          requestCodec,
                                          responseCodec,
                                          allowedMethods,
                                          satisfied =>
                                          {
                                              lock (_pendingLock)
                                                  _pending.Remove(satisfied);
                                          });
                lock (_pendingLock)
                    _pending.Add(entity);

                match.RequestHandler(entity, match.TemplateMatch);
            }
            catch (Exception exception)
            {
                _log.ErrorException(exception, "Unhandled exception while processing http request");
                InternalServerError(context);
            }
        }

        private List<UriToActionMatch> AllMatches(Uri requestUri)
        {
            var matches = new List<UriToActionMatch>();
            foreach (var kvp in _actions)
            {
                var uriTemplateMatch = MatchUriTemplate(kvp.Key.UriTemplate, requestUri);
                if (uriTemplateMatch != null)
                    matches.Add(new UriToActionMatch(uriTemplateMatch, kvp.Key, kvp.Value));
            }
            return matches;
        }

        private UriTemplateMatch MatchUriTemplate(string pattern, Uri uri)
        {
            var template = new UriTemplate(pattern);
            return template.Match(new UriBuilder(uri.Scheme, uri.Host, uri.Port).Uri, uri);
        }

        private void MethodNotAllowed(HttpListenerContext context, string[] allowed)
        {
            var entity = CreateEntity(DateTime.UtcNow, context, Codec.NoCodec, Codec.NoCodec, allowed, _ => { });
            entity.Manager.ReplyStatus(HttpStatusCode.MethodNotAllowed,
                                 "Requested method is not allowed for requested url",
                                 e => _log.ErrorException(e, "Error while closing http connection (http service core)"));
        }

        private void RespondWithOptions(HttpListenerContext context, string[] allowed)
        {
            var entity = CreateEntity(DateTime.UtcNow, context, Codec.NoCodec, Codec.NoCodec, allowed, _ => { });
            entity.Manager.ReplyStatus(HttpStatusCode.OK,
                                 "OK",
                                 e => _log.ErrorException(e, "Error while closing http connection (http service core)"));
        }

        private void NotFound(HttpListenerContext context)
        {
            var entity = CreateEntity(DateTime.UtcNow, context, Codec.NoCodec, Codec.NoCodec, new string[0], _ => { });
            entity.Manager.ReplyStatus(HttpStatusCode.NotFound,
                                 "Not Found",
                                 e => _log.ErrorException(e, "Error while closing http connection (http service core)"));
        }

        private void InternalServerError(HttpListenerContext context)
        {
            var entity = CreateEntity(DateTime.UtcNow, context, Codec.NoCodec, Codec.NoCodec, new string[0], _ => { });
            entity.Manager.ReplyStatus(HttpStatusCode.InternalServerError,
                                 "Internal Server Error",
                                 e => _log.ErrorException(e, "Error while closing http connection (http service core)"));
        }

        private void BadCodec(HttpListenerContext context, string reason)
        {
            var entity = CreateEntity(DateTime.UtcNow, context, Codec.NoCodec, Codec.NoCodec, new string[0], _ => { });
            entity.Manager.ReplyStatus(HttpStatusCode.UnsupportedMediaType,
                                 reason,
                                 e => _log.ErrorException(e, "Error while closing http connection (http service core)"));
        }

        private HttpEntity CreateEntity(DateTime receivedTime,
                                        HttpListenerContext context,
                                        ICodec requestCodec,
                                        ICodec responseCodec,
                                        string[] allowedMethods,
                                        Action<HttpEntity> onRequestSatisfied)
        {
            return new HttpEntity(receivedTime,
                                  requestCodec,
                                  responseCodec,
                                  context,
                                  allowedMethods,
                                  onRequestSatisfied);
        }

        private bool TrySelectRequestCodec(string method,
                                           string contentType,
                                           IEnumerable<ICodec> supportedCodecs,
                                           out ICodec requestCodec)
        {
            if (method != HttpMethod.Post && method != HttpMethod.Put && method != HttpMethod.Delete)
            {
                requestCodec = Codec.NoCodec;
                return true;
            }

            requestCodec = supportedCodecs.SingleOrDefault(c => c.CanParse(contentType));
            return requestCodec != null;
        }


        private bool TrySelectResponseCodec(NameValueCollection query,
                                            ICollection<string> acceptTypes,
                                            IEnumerable<ICodec> supported,
                                            ICodec @default,
                                            out ICodec selected)
        {
            var requestedFormat = GetFormatOrDefault(query);
            if (requestedFormat == null && (acceptTypes == null || acceptTypes.Count == 0))
            {
                selected = @default;
                return true;
            }

            if (requestedFormat != null)
            {
                selected = supported.FirstOrDefault(c => c.SuitableForReponse(new AcceptComponent(requestedFormat)));
                return selected != null;
            }
            var parsedAcceptTypes =
                acceptTypes.Select(ParseAcceptHeaderComponent)
                           .Where(v => v != null)
                           .OrderByDescending(v => v.Priority)
                           .ToArray();

            selected = parsedAcceptTypes.Select(type => supported.FirstOrDefault(c => c.SuitableForReponse(type)))
                                 .FirstOrDefault(corresponding => corresponding != null);
            if (selected != null)
            {
                return true;
            }

            return false;
        }

        private AcceptComponent ParseAcceptHeaderComponent(string v)
        {
            try
            {
                return new AcceptComponent(v);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        private string GetFormatOrDefault(NameValueCollection query)
        {
            var format = query != null && query.Count > 0 ? query.Get("format") : null;
            if (format == null)
                return null;
            switch (format)
            {
                case "json":
                    return ContentType.Json;
                case "text":
                    return ContentType.PlainText;
                case "xml":
                    return ContentType.Xml;
                case "atom":
                    return ContentType.Atom;
                case "atomxj":
                    return ContentType.AtomJson;
                case "atomsvc":
                    return ContentType.AtomServiceDoc;
                case "atomsvcxj":
                    return ContentType.AtomServiceDocJson;
                default:
                    throw new NotSupportedException("Unknown format requested");
            }
        }
        class ProcessHttpHandler : IHandle<IncomingHttpRequestMessage>
        {
            private readonly HttpService _parent;

            public ProcessHttpHandler(HttpService parent)
            {
                _parent = parent;
            }

            public void Handle(IncomingHttpRequestMessage message)
            {
                _parent.ProcessRequest(message.Sender, message.Context);
            }
        }
        class IncomingHttpRequestMessage : Message
        {
            public readonly HttpAsyncServer Sender;
            public readonly HttpListenerContext Context;

            public IncomingHttpRequestMessage(HttpAsyncServer sender, HttpListenerContext context)
            {
                Sender = sender;
                Context = context;
            }
        }
    }
}