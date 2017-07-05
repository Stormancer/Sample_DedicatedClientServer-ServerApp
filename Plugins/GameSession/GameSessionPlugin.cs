using Stormancer.Core;
using Stormancer.Plugins;
using Stormancer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Server.Plugins.API;
using Stormancer.Server;
using Server.Users;
using Stormancer.Server.Components;
using Server.Plugins.Configuration;

namespace Server.Plugins.GameSession
{
    class GameSessionPlugin : IHostPlugin
    {
        internal const string METADATA_KEY = "stormancer.gamesession";

        public void Build(HostPluginBuildContext ctx)
        {
            ctx.SceneDependenciesRegistration += (IDependencyBuilder builder, ISceneHost scene) =>
              {
                  if (scene.Metadata.ContainsKey(METADATA_KEY))
                  {
                      builder.Register<GameSessionService>().As<IGameSessionService>().SingleInstance();
                      builder.Register<GameSessionController>().InstancePerRequest();
                  }
              };
            ctx.HostStarted += (IHost host) =>
            {

            };
            ctx.SceneCreated += (ISceneHost scene) =>
             {
                 if (scene.Metadata.ContainsKey(METADATA_KEY))
                 {
                     scene.AddController<GameSessionController>();
                     scene.Starting.Add(metadata =>
                     {

                         var service = scene.DependencyResolver.Resolve<IGameSessionService>();
                         service.SetConfiguration(metadata);

                         return Task.FromResult(true);

                     });
                 }
             };
        }
    }
}
