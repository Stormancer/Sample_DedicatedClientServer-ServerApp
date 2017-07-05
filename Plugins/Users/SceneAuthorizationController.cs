using Server.Plugins.API;
using Stormancer;
using Stormancer.Diagnostics;
using Stormancer.Plugins;
using System.Threading.Tasks;

namespace Server.Users
{
    internal class SceneAuthorizationController : ControllerBase
    {
        private readonly Management.ManagementClientAccessor _accessor;
        private readonly UserManagementConfig _config;
        private readonly IUserSessions _sessions;
        private readonly ILogger _logger;

        public SceneAuthorizationController(Management.ManagementClientAccessor accessor, UserManagementConfig config, IUserSessions sessions, ILogger logger)
        {
            _logger = logger;
            _accessor = accessor;
            _config = config;
            _sessions = sessions;
        }
        public async Task GetToken(RequestContext<IScenePeerClient> ctx)
        {
            _logger.Log(LogLevel.Debug, "authorization", "GetToken called.", new {});

            var client = await _accessor.GetApplicationClient();

            var user = await _sessions.GetUser(ctx.RemotePeer);
            var sceneId = ctx.ReadObject<string>();
            _logger.Log(LogLevel.Debug, "authorization", $"Authorizing access to scene '{sceneId}'", new { sceneId, user.Id });
            var token = await client.CreateConnectionToken(sceneId, new byte[0], "application/octet-stream");

            ctx.SendValue(token);
        }
    }
}