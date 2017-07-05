using Server.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stormancer.Core;
using Server.Plugins.Configuration;
using Stormancer.Diagnostics;

namespace Server.Users
{
    class AdminImpersonationAuthenticationProvider : IAuthenticationProvider
    {
        private const string Provider_Name = "impersonation";
        private string _secret;
        private bool _isEnabled;
        private ILogger logger;
        public void AddMetadata(Dictionary<string, string> result)
        {
            //Don't add metadata.
        }

        public void Initialize(ISceneHost scene)
        {
            var config = scene.DependencyResolver.Resolve<IConfiguration>();
            logger = scene.DependencyResolver.Resolve<ILogger>();

            config.SettingsChanged += (s, e) => ApplyConfiguration(config);
            ApplyConfiguration(config);
        }

        

        private void ApplyConfiguration(IConfiguration configuration)
        {
            var auth = configuration.Settings.auth;
            if (auth != null)
            {
                var impersonation = auth.adminImpersonation;
                if(impersonation !=null)
                {
                    if(impersonation.enabled != null)
                    {
                        try
                        {
                            _isEnabled = (bool)impersonation.enabled;
                        }
                        catch {
                            _isEnabled = false;
                            logger.Error("users.adminImpersonation", "Failed to load auth.adminImpersonation.enabled (bool) config parameter. Impersonation disabled.");
                        }
                    }
                    if(impersonation.secret != null)
                    {
                        try
                        {
                            _secret = (string)impersonation.secret;
                        }
                        catch {
                            _isEnabled = false;
                            logger.Error("users.adminImpersonation", "Failed to load auth.adminImpersonation.secret (string) config parameter. Impersonation disabled.");
                        }
                    }
                }
            }
        }

        public async Task<AuthenticationResult> Authenticate(Dictionary<string, string> authenticationCtx, IUserService userService)
        {
            if (authenticationCtx["provider"] != Provider_Name)
            {
                return null;
            }
            string secret;
            var pId = new PlatformId { Platform = Provider_Name };
            if (!authenticationCtx.TryGetValue("secret", out secret) || string.IsNullOrWhiteSpace(secret))
            {
                return AuthenticationResult.CreateFailure("Missing impersonation secret.", pId, authenticationCtx);
            }

            if (secret != _secret)
            {
                return AuthenticationResult.CreateFailure("Invalid impersonation secret.", pId, authenticationCtx);
            }

            string ImpersonatingProvider;
            string ImpersonatingClaimPath;
            string ImpersonatingClaimValue;
            if (!authenticationCtx.TryGetValue("impersonated-provider", out ImpersonatingProvider) || string.IsNullOrWhiteSpace(ImpersonatingProvider))
            {
                return AuthenticationResult.CreateFailure("'impersonated-provider' must not be empty.", pId, authenticationCtx);
            }

            if (!authenticationCtx.TryGetValue("claimPath", out ImpersonatingClaimPath) || string.IsNullOrWhiteSpace(ImpersonatingClaimPath))
            {
                return AuthenticationResult.CreateFailure("'claimPath' must not be empty.", pId, authenticationCtx);
            }
            if (!authenticationCtx.TryGetValue("claimValue", out ImpersonatingClaimValue) || string.IsNullOrWhiteSpace(ImpersonatingClaimValue))
            {
                return AuthenticationResult.CreateFailure("'claimValue' must not be empty.", pId, authenticationCtx);
            }
            var user = await userService.GetUserByClaim(ImpersonatingProvider, ImpersonatingClaimPath, ImpersonatingClaimValue);

            if (user == null)
            {
                return AuthenticationResult.CreateFailure($"The user '{ImpersonatingProvider}/{ImpersonatingClaimPath} = {ImpersonatingClaimValue}' does not exist.", pId, authenticationCtx);
            }
            else
            {
                return AuthenticationResult.CreateSuccess(user, new PlatformId { Platform = ImpersonatingProvider, OnlineId = ImpersonatingClaimValue }, authenticationCtx);
            }
        }

      
    }
}
