using Stormancer;
using Stormancer.Core;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Plugins.GameSession
{
    public interface IGameSessionEventHandler
    {
        Task GameSessionStarted(GameSessionStartedCtx ctx);

        Task GameSessionCompleted(GameSessionCompleteCtx ctx);
    }

    public class GameSessionContext
    {
        public GameSessionContext(ISceneHost scene)
        {
            Scene = scene;
        }

        public ISceneHost Scene { get; }
    }

    public class GameSessionStartedCtx : GameSessionContext
    {
        public GameSessionStartedCtx(ISceneHost scene, IEnumerable<Player> peers) : base(scene)
        {
            Peers = peers;
        }
        public IEnumerable<Player> Peers { get; }
    }

    public class Player
    {
        public Player(IScenePeerClient client, string uid)
        {
            UserId = uid;
            Peer = client;
        }
        public IScenePeerClient Peer { get; }

        public string UserId { get; }
    }

    public class GameSessionCompleteCtx : GameSessionContext
    {
        public GameSessionCompleteCtx(ISceneHost scene, IEnumerable<GameSessionResult> results, IEnumerable<string> players) : base(scene)
        {
            Results = results;
            PlayerIds = players;
            ResultsWriter = (p,s) => { };
        }

        public IEnumerable<GameSessionResult> Results { get; }

        public IEnumerable<string> PlayerIds { get; }
        public Action<Stream, ISerializer> ResultsWriter { get; set; }
    }

    public class GameSessionResult
    {
        public GameSessionResult(string userId,IScenePeerClient client, Stream data)
        {
            Peer = client;
            Data = data;
            UserId = userId;
        }
        public IScenePeerClient Peer { get; }

        public string UserId { get;  }

        public Stream Data { get; }

        
    }

}
