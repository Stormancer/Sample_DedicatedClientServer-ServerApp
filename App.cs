using Stormancer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Server.Plugins.API;
using Stormancer.Diagnostics;
using Stormancer.Configuration;
using Newtonsoft.Json.Linq;
using Server.Plugins.Configuration;
using Server.Plugins.GameSession;
using DedicatedSample;

namespace Server
{
    public class App
    {
        public const string GAMESESSION_TEMPLATE = "world-shard";
        public const string LOCATOR_TEMPLATE = "locator";

        public void Run(IAppBuilder builder)
        {
            builder.AddPlugin(new ConfigurationManagerPlugin());
          


            

            builder.SceneTemplate(LOCATOR_TEMPLATE, scene =>
            {
                scene.AddLocator();   

            });

            builder.SceneTemplate(GAMESESSION_TEMPLATE, scene =>
            {
                scene.AddGameSession();
            });


           
        }
    }
}
