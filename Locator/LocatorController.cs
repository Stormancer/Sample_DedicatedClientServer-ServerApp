using Newtonsoft.Json.Linq;
using Server.Management;
using Server.Plugins.API;
using Stormancer;
using Stormancer.Diagnostics;
using Stormancer.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace DedicatedSample
{
    public class LocatorController : ControllerBase
    {
        private readonly ManagementClientAccessor _management;
        private readonly ILogger _logger;

        public LocatorController(ManagementClientAccessor management, ILogger logger)
        {
            _management = management;
            _logger = logger;
        }

        public async Task GetShard(RequestContext<IScenePeerClient> ctx)
        {
            var client = await _management.GetApplicationClient();
            // Get data send by client
            var shardID = ctx.ReadObject<string>();

            var shard = await client.GetScene(shardID);

            if (shard == null)
            {
                var metadata = Newtonsoft.Json.Linq.JObject.FromObject(new { gameSession = new Server.Plugins.GameSession.GameSessionConfiguration { Public = true, canRestart = true, UserData = shardID } });

                var template = global::Server.App.GAMESESSION_TEMPLATE;

                _logger.Log(LogLevel.Trace, "LocatorController", "Shard ID call on getshard : ", shardID);

                await client.CreateScene(
                    shardID,
                    template,
                    false,
                    metadata,
                    true);
            }
            var token = await client.CreateConnectionToken(shardID, "");

            ctx.SendValue(token);
        }
    }
}
