using MCloudServer.Controllers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace MCloudServer
{
    public class MessageService
    {
        private readonly DbCtx dbCtx;
        private readonly IServiceScope scope;
        private readonly ILogger<MessageService> logger;

        private readonly List<Client> clients;

        public MessageService(IServiceProvider services, ILogger<MessageService> logger)
        {
            this.scope = services.CreateScope();
            this.dbCtx = scope.ServiceProvider.GetService<DbCtx>();
            this.logger = logger;
            this.clients = new List<Client>();
        }

        public Task HandleWebSocket(WebSocket ws)
        {
            var client = new Client(this, ws);
            return client.Handle();
        }

        public void TriggerEvent(string evt, Func<Client, bool> condition = null)
        {
            TriggerEvent<object>(evt, condition, null);
        }

        public void TriggerEvent<T>(string evt, Func<Client, bool> condition, T data)
        {
            lock (this.clients)
            {
                foreach (var c in this.clients)
                {
                    if (c.ListeningEvents.Contains(evt) && (condition == null || condition(c)))
                    {
                        c.SendEvent(evt, data);
                    }
                }
            }
        }

        private IServiceScope CreateScope()
        {
            return scope.ServiceProvider.CreateScope();
        }



        public class Client
        {
            private readonly MessageService service;
            private readonly WebSocket ws;

            public string Id { get; } = Guid.NewGuid().ToString("D");
            public string Token { get; set; }
            public string Name { get; set; } = "Client";
            public User User { get; private set; }
            public List<string> ListeningEvents { get; } = new List<string>();

            public TrackLocationWithProfile NowPlaying { get; set; }
            public bool Paused { get; set; }

            public Client(MessageService service, WebSocket ws)
            {
                this.service = service;
                this.ws = ws;
            }

            public async Task Handle()
            {
                try
                {
                    while (true)
                    {
                        var jsonDoc = await ReceiveJson();
                        if (jsonDoc == null)
                        {
                            await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "close normally", CancellationToken.None);
                            return;
                        }
                        var json = jsonDoc.RootElement;
                        var queryId = 0;
                        if (json.TryGetProperty("queryId", out var q))
                        {
                            queryId = q.GetInt32();
                        }
                        object response = null;
                        try
                        {
                            response = await HandleMessage(json, queryId);
                        }
                        catch (Exception ex)
                        {
                            service.logger.LogError(ex, "Error handling message.");
                            response = new { resp = "error", queryId };
                        }

                        if (response != null)
                        {
                            await SendJson(response, response.GetType());
                        }
                    }
                }
                catch (OperationCanceledException) { }
                catch (Exception ex)
                {
                    service.logger.LogError(ex, "Error in receiving loop.");
                }
                finally
                {
                    if (this.User != null) SetUser(null, null);
                }
            }

            public async Task<object> HandleMessage(JsonElement json, int queryId)
            {
                var cmd = json.GetProperty("cmd").GetString();
                if (cmd == "login")
                {
                    var token = json.GetProperty("token").GetString();
                    if (json.TryGetProperty("name", out var name))
                    {
                        Name = name.GetString();
                    }

                    using (var scope = service.CreateScope())
                    {
                        var dbctx = scope.ServiceProvider.GetService<DbCtx>();
                        var r = await UserService.GetLoginFromToken(dbctx, token);
                        if (r.User != null)
                        {
                            SetUser(r.User, token);
                            return new
                            {
                                resp = "ok",
                                queryId,
                                uid = r.User.id,
                                username = r.User.username
                            };
                        }
                        else
                        {
                            return new { resp = "fail", queryId };
                        }
                    }
                }
                else if (cmd == "listenEvent")
                {
                    var evts = json.GetProperty("events").EnumerateArray()
                        .Select(x => x.GetString()).ToList();
                    AddEvent(evts);
                    return new { resp = "ok", queryId };
                }
                else if (cmd == "getClients")
                {
                    if (User == null) return new { resp = "fail", queryId, reason = "no_login" };
                    return new
                    {
                        resp = "ok",
                        queryId,
                        clients = GetClients().Select(c => c.GetInfo())
                    };
                }
                else if (cmd == "playingState")
                {
                    if (User == null) return new { resp = "fail", queryId, reason = "no_login" };
                    NowPlaying = JsonSerializer.Deserialize<TrackLocationWithProfile>(
                        json.GetProperty("nowPlaying").GetRawText()
                    );
                    Paused = json.GetProperty("paused").GetBoolean();
                    var justStarted = json.GetProperty("justStarted").GetBoolean();
                    if (justStarted) {
                        using (var scope = service.CreateScope()) {
                            var dbctx = scope.ServiceProvider.GetService<DbCtx>();
                            var user = await dbctx.Users.FindAsync(User.id);
                            await UsersController.PostPlaying(dbctx, user, NowPlaying);
                            this.User = user;
                        }
                    }
                    return new { resp = "ok", queryId };
                }
                else
                {
                    return new
                    {
                        resp = "unknownCmd",
                        queryId,
                        unknownCmd = cmd
                    };
                }
            }

            private void AddEvent(List<string> evts)
            {
                lock (service.clients)
                {
                    foreach (var e in evts)
                    {
                        if (!ListeningEvents.Contains(e))
                        {
                            ListeningEvents.Add(e);
                        }
                    }
                }
            }

            private List<Client> GetClients()
            {
                lock (service.clients)
                {
                    return service.clients.Where(x => x.User.id == this.User.id).ToList();
                }
            }

            class Info
            {
                public string id;
                public TrackLocation playing;
            }

            private Info GetInfo()
            {
                return new Info
                {
                    id = this.Id,
                    playing = this.NowPlaying
                };
            }

            public void SetUser(User user, string token)
            {
                if (this.User == user) return;
                lock (service.clients)
                {
                    if (this.User == null)
                    {
                        service.clients.Add(this);
                    }
                    if (user == null)
                    {
                        service.clients.Remove(this);
                    }
                    this.User = user;
                    this.Token = token;
                    // TODO: add/removeClient event
                }
            }

            public struct ReceiveResult
            {
                public ArraySegment<byte> Buffer;
                public WebSocketMessageType Type;
            }

            public async Task<ReceiveResult> Receive()
            {
                var buf = new byte[64 * 1024];
                var haveRead = 0;
                ValueWebSocketReceiveResult r;
                do
                {
                    r = await ws.ReceiveAsync(buf.AsMemory(haveRead), CancellationToken.None);
                    if (r.MessageType == WebSocketMessageType.Close)
                        return default;
                    haveRead += r.Count;
                } while (r.EndOfMessage == false);
                return new ReceiveResult
                {
                    Buffer = new ArraySegment<byte>(buf, 0, haveRead),
                    Type = r.MessageType
                };
            }

            public async Task<string> ReceiveString()
            {
                var r = await Receive();
                if (r.Type != WebSocketMessageType.Text)
                    throw new Exception("unexpected MessageType: " + r.Type);
                return Encoding.UTF8.GetString(r.Buffer);
            }

            public async Task<JsonDocument> ReceiveJson()
            {
                var r = await Receive();
                if (r.Buffer.Count == 0) return null;
                if (r.Type != WebSocketMessageType.Text)
                    throw new Exception("unexpected MessageType: " + r.Type);
                return JsonDocument.Parse(r.Buffer);
            }

            public Task SendEvent(string evt)
            {
                return SendJson(new
                {
                    cmd = "event",
                    @event = evt
                });
            }

            public Task SendEvent<T>(string evt, T data)
            {
                return SendJson(new
                {
                    cmd = "event",
                    @event = evt,
                    data
                });
            }

            public Task SendJson<T>(T obj)
            {
                var buf = JsonSerializer.SerializeToUtf8Bytes<T>(obj);
                return Send(buf, WebSocketMessageType.Text, true, CancellationToken.None);
            }

            public Task SendJson(object obj, Type type)
            {
                var buf = JsonSerializer.SerializeToUtf8Bytes(obj, type);
                return Send(buf, WebSocketMessageType.Text, true, CancellationToken.None);
            }

            SemaphoreSlim semaphore = new SemaphoreSlim(1, 1);

            public async Task Send(byte[] buf, WebSocketMessageType typ, bool endOfMessage, CancellationToken ct)
            {
                await semaphore.WaitAsync();
                try
                {
                    await ws.SendAsync(buf, typ, endOfMessage, CancellationToken.None);
                }
                finally
                {
                    semaphore.Release();
                }
            }
        }
    }
}
