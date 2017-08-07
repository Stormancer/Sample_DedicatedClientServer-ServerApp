using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stormancer.Core;
using Stormancer.Plugins;
using Stormancer.Server;
using Stormancer;
using Stormancer.Diagnostics;
using Server.Plugins.API;

namespace Server.Users
{
    class UsersManagementPlugin : Stormancer.Plugins.IHostPlugin
    {
        private readonly UserManagementConfig _config;

        public UsersManagementPlugin(UserManagementConfig config = null)
        {
            if (config == null)
            {
                config = new UserManagementConfig();
            }
            _config = config;

        }
        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostStarting += HostStarting;
			ctx.HostStarted += HostStarted;
            ctx.HostDependenciesRegistration += RegisterDependencies;

        }
        private void RegisterDependencies(IDependencyBuilder b)
        {
            //Indices
            b.Register<UserPeerIndex>().As<IUserPeerIndex>().SingleInstance();
            b.Register<PeerUserIndex>().As<IPeerUserIndex>().SingleInstance();
            b.Register<UserToGroupIndex>().SingleInstance();
            b.Register<GroupsIndex>().SingleInstance();
            b.Register<SingleNodeActionStore>().As<IActionStore>().SingleInstance();
            b.Register<SceneAuthorizationController>();
            b.Register<UserManagementConfig>(_config);

            b.Register<UserService>().As<IUserService>();
            b.Register<UserSessions>().As<IUserSessions>();

            

        }
        private void HostStarting(IHost host)
        {
            host.AddSceneTemplate("authenticator", AuthenticatorSceneFactory);


        }
		
		private void HostStarted(IHost host)
        {
            var managementAccessor = host.DependencyResolver.Resolve<Management.ManagementClientAccessor>();
            if(managementAccessor!=null)
            {
                managementAccessor.GetApplicationClient().ContinueWith(async t => {

                    var client = await t;
                    await client.CreateScene("authenticator", "authenticator");
                });
            }
        }



        private void AuthenticatorSceneFactory(ISceneHost scene)
        {
            scene.AddProcedure("login", async p =>
            {
                try
                {
                    //scene.GetComponent<ILogger>().Log(LogLevel.Trace, "user.login", "Logging in an user.", null);

                    var accessor = scene.DependencyResolver.Resolve<Management.ManagementClientAccessor>();
                    var authenticationCtx = p.ReadObject<Dictionary<string, string>>();
                    //scene.GetComponent<ILogger>().Log(LogLevel.Trace, "user.login", "Authentication context read.", authenticationCtx);
                    var result = new LoginResult();
                    var userService = scene.DependencyResolver.Resolve<IUserService>();
                    var userSessions = scene.DependencyResolver.Resolve<IUserSessions>();
                    var handled = false;
                    foreach (var provider in _config.AuthenticationProviders)
                    {
                        var authResult = await provider.Authenticate(authenticationCtx, userService);
                        if (authResult == null)
                        {
                            continue;
                        }
                        handled = true;
                        if (authResult.Success)
                        {
                            //scene.GetComponent<ILogger>().Log(LogLevel.Trace, "user.login", "Authentication successful.", authResult);
                            var oldPeer = await userSessions.GetPeer(authResult.AuthenticatedUser.Id);
                            if (oldPeer != null)
                            {
                                await oldPeer.DisconnectFromServer("User connected elsewhere");
                            }

                            await userSessions.Login(p.RemotePeer, authResult.AuthenticatedUser, authResult.PlatformId);

                            result.Success = true;
                            var client = await accessor.GetApplicationClient();
                            result.UserId = authResult.AuthenticatedUser.Id;
                            result.Username = authResult.Username;
                            break;
                        }
                        else
                        {
                            //scene.GetComponent<ILogger>().Log(LogLevel.Trace, "user.login", "Authentication failed.", authResult);

                            result.ErrorMsg = authResult.ReasonMsg;
                            break;
                        }

                    }
                    if (!handled)
                    {
                        scene.DependencyResolver.Resolve<ILogger>().Log(LogLevel.Error, "UsersManagement.login", "Login failed: provider not found ", authenticationCtx);
                        result.ErrorMsg = "No authentication provider able to handle these credentials were found.";

                    }

                    p.SendValue(result);
                    if (!result.Success)
                    {
                        var _ = Task.Delay(2000).ContinueWith(t => p.RemotePeer.DisconnectFromServer("Authentication failed"));
                    }
                }
                catch (Exception ex)
                {
                    scene.DependencyResolver.Resolve<ILogger>().Log(LogLevel.Error, "UsersManagement.login", "an exception occurred while trying to log in a user", ex);
                    throw;
                }
            });

            //scene.AddController<GroupController>();
            scene.AddController<SceneAuthorizationController>();
            scene.Disconnected.Add(async args =>
            {
                await scene.GetComponent<IUserSessions>().LogOut(args.Peer);
            });

            scene.Starting.Add(_ =>
            {
                foreach (var provider in _config.AuthenticationProviders)
                {
                    provider.Initialize(scene);
                }
                return Task.FromResult(true);
            });


        }
        private Dictionary<string, string> GetAuthenticateRouteMetadata()
        {
            var result = new Dictionary<string, string>();

            foreach (var provider in _config.AuthenticationProviders)
            {
                provider.AddMetadata(result);
            }

            return result;
        }
    }




}
