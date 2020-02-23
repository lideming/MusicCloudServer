using Microsoft.Extensions.DependencyInjection;
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
        private readonly IServiceScope scope;
        private readonly ILogger<MessageService> logger;

        public MessageService(IServiceProvider services, ILogger<MessageService> logger)
        {
            this.scope = services.CreateScope();
            this.dbCtx = scope.ServiceProvider.GetService<DbCtx>();
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
