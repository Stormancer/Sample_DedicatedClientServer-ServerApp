using Stormancer.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stormancer;

namespace Server.Plugins.Steam
{
    class SteamPlugin : IHostPlugin
    {
        public void Build(HostPluginBuildContext ctx)
        {
            ctx.HostDependenciesRegistration += RegisterDependencies;
        }

        private void RegisterDependencies(IDependencyBuilder builder)
        {
           
            builder.Register<SteamService>().As<ISteamService>().InstancePerScene();
        }
    }
}
