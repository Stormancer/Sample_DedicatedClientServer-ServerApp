//using Battlefleet.Server.Profiles;
//using Stormancer;
//using Stormancer.Plugins;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Threading.Tasks;


//namespace Stormancer
//{
//    public static class AppBuilderExtensions
//    {
//        public static void ConfigureRegistrations(this IAppBuilder appBuilder, Action<IDependencyBuilder> dependencyBuilderAction)
//        {
//            appBuilder.AddPlugin(new RegistrationsPlugin(dependencyBuilderAction));
//        }

//        private class RegistrationsPlugin:IHostPlugin
//        {
//            private Action<IDependencyBuilder> dependencyBuilderAction;
//            public RegistrationsPlugin(Action<IDependencyBuilder> dependencyBuilderAction)
//            {
//                this.dependencyBuilderAction = dependencyBuilderAction;
//            }

//            public void Build(HostPluginBuildContext ctx)
//            {
//                ctx.HostDependenciesRegistration += dependencyBuilderAction;
//            }
//        }
//    }
//}
