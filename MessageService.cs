using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading.Tasks;

namespace MCloudServer
{
    public class MessageService
    {
        private readonly DbCtx dbCtx;
        private readonly ILogger<MessageService> logger;

        public MessageService(DbCtx dbCtx, ILogger<MessageService> logger)
        {
            this.dbCtx = dbCtx;
            this.logger = logger;
        }

        public async Task HandleWebSocket(WebSocket ws)
        {
        }

        class Client
        {
            private readonly MessageService service;
            private readonly WebSocket ws;

            public Client(MessageService service, WebSocket ws)
            {
                this.service = service;
                this.ws = ws;
            }
        }
    }
}
