using Stormancer;
using Stormancer.Core;
using Stormancer.Plugins;
using Stormancer.Server;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;


namespace DedicatedSample
{
    public class App
    {
        public void Run(IAppBuilder builder)
        {
            builder.AddPlugin(new LocatorPlugin());
        }
    }

    class LocatorPlugin : IHostPlugin
    {
        internal const string METADATA_KEY = "sample.locator";

        public void Build(HostPluginBuildContext ctx)
        {
            ctx.SceneDependenciesRegistration += (IDependencyBuilder builder, ISceneHost scene) =>
            {
                if (scene.Metadata.ContainsKey(METADATA_KEY))
                {
                    builder.Register<LocatorController>().InstancePerRequest();
                }
            };
           
            ctx.SceneCreated += (ISceneHost scene) =>
            {
                if (scene.Metadata.ContainsKey(METADATA_KEY))
                {
                    scene.AddController<LocatorController>();
                }
               
            };
        }
    }

    public static class LocatorExtensions
    {
        public static void AddLocator(this ISceneHost scene)
        {
            scene.Metadata[DedicatedSample.LocatorPlugin.METADATA_KEY] = "enabled";
        }
    }
}
