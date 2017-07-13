using Server.Management;
using Server.Plugins.API;
using Stormancer;
using Stormancer.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DedicatedSample
{
    public class LocatorController : ControllerBase
    {
        private readonly ManagementClientAccessor _management;
        public const string SHARD_ID = "sample-shard";
        public LocatorController(ManagementClientAccessor management)
        {
            _management = management;
        }
        public async Task GetShard(RequestContext<IScenePeerClient> ctx)
        {
            var client = await _management.GetApplicationClient();

            var shard = await client.GetScene(SHARD_ID);

            if (shard == null)
            {
                var metadata = Newtonsoft.Json.Linq.JObject.FromObject(new { gameSession = new Server.Plugins.GameSession.GameSessionConfiguration { Public = true } });

                var template = global::Server.App.GAMESESSION_TEMPLATE;

                await client.CreateScene(
                    SHARD_ID,
                    template,
                    false,
                    metadata,
                    true);
            }
            var token = await client.CreateConnectionToken(SHARD_ID, "");

            ctx.SendValue(token);
        }
    }
}
