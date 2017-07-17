using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stormancer.Core;
using Newtonsoft.Json.Linq;
using System.Net.Http;
using System.IO;
using Stormancer.Server.Components;
using Server.Plugins.Steam;
using Stormancer.Diagnostics;
using Stormancer;
using System.Collections.Concurrent;

namespace Server.Users
{
    public class TestAuthenticationProvider : IAuthenticationProvider, IUserSessionEventHandler
    {
        private ConcurrentDictionary<ulong, string> _vacSessions = new ConcurrentDictionary<ulong, string>();
        public const string PROVIDER_NAME = "test";
        private const string ClaimPath = "test";

        private bool _enabled;
        private List<string> _blackList;

        private ILogger _logger;

        public TestAuthenticationProvider()
        {
        }

        public void AddMetadata(Dictionary<string, string> result)
        {
            result.Add("provider.testAuthentication", "enabled");
        }

        public void Initialize(ISceneHost scene)
        {
            var environment = scene.DependencyResolver.Resolve<IEnvironment>();
            _logger = scene.DependencyResolver.Resolve<ILogger>();
            ApplyConfig(environment, scene);

            environment.ConfigurationChanged += (sender, e) => ApplyConfig(environment, scene);
        }

        private void ApplyConfig(IEnvironment environment, ISceneHost scene)
        {
            var testConfig = environment.Configuration.auth?.test;

            _enabled = (bool?)testConfig?.enabled ?? false;
           
            _blackList = ((JValue)testConfig?.blackList)?.ToObject<List<string>>() ?? new List<string>();

        }


        public async Task<AuthenticationResult> Authenticate(Dictionary<string, string> authenticationCtx, IUserService userService)
        {

            if (authenticationCtx["provider"] != PROVIDER_NAME)
            {
                return null;
            }

            string ticket;
            var pId = new PlatformId { Platform = PROVIDER_NAME };
            if (!_enabled)
            {
                return AuthenticationResult.CreateFailure("Provider disabled", pId, authenticationCtx);
            }

            if (!authenticationCtx.TryGetValue("login", out ticket) || string.IsNullOrWhiteSpace(ticket))
            {
                return AuthenticationResult.CreateFailure("'login' field cannot be empty.", pId, authenticationCtx);
            }
            try
            {
                if (_blackList.Contains(ticket))
                {
                    return AuthenticationResult.CreateFailure("Login blacklisted.", pId, authenticationCtx);
                }
                else
                {
                    var user = await userService.GetUserByClaim(PROVIDER_NAME, ClaimPath, ticket);
                    if (user == null)
                    {
                        var uid = Guid.NewGuid().ToString("N");

                        user = await userService.CreateUser(uid, JObject.FromObject(new { login = ticket, pseudo = ticket }));

                        var claim = new JObject();
                        claim[ClaimPath] = ticket;
                        user = await userService.AddAuthentication(user, PROVIDER_NAME, claim, ticket);
                    }
                    return AuthenticationResult.CreateSuccess(user, pId, authenticationCtx);
                }

              
            
                
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Debug, "authenticator.test", $"Test authentication failed. Ticket : {ticket}", ex);
                return AuthenticationResult.CreateFailure($"Test auth failed.", pId, authenticationCtx);
            }
        }

        public async Task OnLoggedIn(IScenePeerClient client, User user, PlatformId platformId)
        {
            await Task.FromResult(true);
        }

        public async Task OnLoggedOut(long clientId, User user)
        {
            await Task.FromResult(true);

        }
    }
}
