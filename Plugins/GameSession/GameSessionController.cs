using Server.Plugins.API;
using Stormancer;
using Stormancer.Diagnostics;
using Stormancer.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Plugins.GameSession
{
    class GameSessionController : ControllerBase
    {
        private readonly IGameSessionService _service;
        private readonly ILogger _logger;

        public GameSessionController(IGameSessionService service, ILogger logger)
        {
            _service = service;
            _logger = logger;
        }
        public async Task PostResults(RequestContext<IScenePeerClient> ctx)
        {
            var writer = await _service.PostResults(ctx.InputStream, ctx.RemotePeer);
            ctx.SendValue(s =>
            {
                var oldPosition = s.Position;
                writer(s, ctx.RemotePeer.Serializer());
                //_logger.Log(LogLevel.Trace, "gamesession.postresult", "sending response to postresponse message.", new { Length = s.Position - oldPosition });
            });

        }

        public Task Reset(RequestContext<IScenePeerClient> ctx)
        {
            return _service.Reset();
        }

    }
}
