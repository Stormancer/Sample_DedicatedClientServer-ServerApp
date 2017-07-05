using Server.Plugins.GameSession;
using Stormancer.Core;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer
{
    public static class GameSessionsExtensions
    {
        public static void AddGameSession(this ISceneHost scene)
        {
            scene.Metadata[GameSessionPlugin.METADATA_KEY] = "enabled";
        }
    }
}
