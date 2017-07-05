using Stormancer.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stormancer.Server;
using Server.Plugins.Configuration;

namespace Stormancer.Configuration
{
    public class ConfigurationManagerPlugin : IHostPlugin
    {
        public void Build(HostPluginBuildContext ctx)
        {
          
            ctx.HostDependenciesRegistration += RegisterHost;
        }

        private static void RegisterHost(IDependencyBuilder builder)
        {
            builder.Register<ConfigurationService>().SingleInstance();
            builder.Register<EnvironmentConfiguration>().As<IConfiguration>();
        }
    }
}
