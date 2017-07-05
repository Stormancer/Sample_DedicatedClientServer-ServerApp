using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Stormancer;
using Server.Plugins.Database;
using Stormancer.Core;
using Stormancer.Diagnostics;

namespace Server.Users
{
    public interface IUserPeerIndex : IIndex<long> { }
    internal class UserPeerIndex : InMemoryIndex<long>, IUserPeerIndex { }



    public interface IPeerUserIndex : IIndex<Session> { }
    internal class PeerUserIndex : InMemoryIndex<Session>, IPeerUserIndex { }

    public class Session
    {
        public PlatformId platformId { get; set; }
        public User User { get; set; }

        public Dictionary<string, object> SessionData { get; set; }
    }
    public class UserSessions : IUserSessions
    {
        private readonly IUserPeerIndex _userPeerIndex;
        private readonly IUserService _userService;
        private readonly IPeerUserIndex _peerUserIndex;
        private readonly IEnumerable<IUserSessionEventHandler> _eventHandlers;
        private readonly IEnumerable<IAuthenticationProvider> _authProviders;
        private readonly ISceneHost _scene;
        private readonly ILogger logger;


        public UserSessions(IUserService userService,
            IPeerUserIndex peerUserIndex,
            IUserPeerIndex userPeerIndex,
            IEnumerable<IUserSessionEventHandler> eventHandlers,
            IEnumerable<IAuthenticationProvider> authProviders,
            ISceneHost scene, ILogger logger)
        {
            _userService = userService;
            _peerUserIndex = peerUserIndex;
            _userPeerIndex = userPeerIndex;
            _eventHandlers = eventHandlers;
            _scene = scene;
            _authProviders = authProviders;

            this.logger = logger;
        }

        public async Task<User> GetUser(IScenePeerClient peer)
        {
            var session = await GetSession(peer);

            return session?.User;
        }

        public async Task<bool> IsAuthenticated(IScenePeerClient peer)
        {
            return (await GetUser(peer)) != null;
        }

        public Task LogOut(IScenePeerClient peer)
        {

            return LogOut(peer.Id);

        }
        private async Task LogOut(long peerId)
        {
            var result = await _peerUserIndex.TryRemove(peerId.ToString());
            if (result.Success)
            {
                await _userPeerIndex.TryRemove(result.Value.User.Id);
                await _userPeerIndex.TryRemove(result.Value.platformId.ToString());
                await _eventHandlers.RunEventHandler(h => h.OnLoggedOut(peerId, result.Value.User), ex => logger.Log(LogLevel.Error, "usersessions", "An error occured while running LoggedOut event handlers", ex));
                await _userService.LogoutEventOccured(result.Value.User, peerId);
                //logger.Trace("usersessions", $"removed '{result.Value.Id}' (peer : '{peer.Id}') in scene '{_scene.Id}'.");
            }
        }

        public async Task Login(IScenePeerClient peer, User user, PlatformId onlineId)
        {
            if (peer == null)
            {
                throw new ArgumentNullException("peer");
            }
            if (user == null)
            {
                throw new ArgumentNullException("user");
            }

            bool added = false;
            while (!added)
            {
                var r = await _userPeerIndex.GetOrAdd(user.Id, peer.Id);
                if (r.Value != peer.Id)
                {
                    await LogOut(r.Value);
                }
                else
                {
                    added = true;
                }
            }


            await _userPeerIndex.TryAdd(onlineId.ToString(), peer.Id);
            await _peerUserIndex.TryAdd(peer.Id.ToString(), new Session { User = user, platformId = onlineId });
            await _eventHandlers.RunEventHandler(h => h.OnLoggedIn(peer, user, onlineId), ex => logger.Log(LogLevel.Error, "usersessions", "An error occured while running LoggedIn event handlers", ex));
            await _userService.LoginEventOccured(user, peer);
            //logger.Trace("usersessions", $"Added '{user.Id}' (peer : '{peer.Id}') in scene '{_scene.Id}'.");
        }


        public async Task UpdateUserData<T>(IScenePeerClient peer, T data)
        {
            var user = await GetUser(peer);
            if (user == null)
            {
                throw new InvalidOperationException("User not found.");

            }
            else
            {
                user.UserData = Newtonsoft.Json.Linq.JObject.FromObject(data);
                await _userService.UpdateUserData(user.Id, data);
            }
        }

        public async Task<IScenePeerClient> GetPeer(string userId)
        {
            var result = await _userPeerIndex.TryGet(userId);

            if (result.Success)
            {

                var peer = _scene.RemotePeers.FirstOrDefault(p => p.Id == result.Value);
                //logger.Trace("usersessions", $"found '{userId}' (peer : '{result.Value}', '{peer.Id}') in scene '{_scene.Id}'.");
                if (peer == null)
                {
                    //logger.Trace("usersessions", $"didn't found '{userId}' (peer : '{result.Value}') in scene '{_scene.Id}'.");
                }
                return peer;
            }
            else
            {
                //logger.Trace("usersessions", $"didn't found '{userId}' in userpeer index.");
                return null;
            }
        }
        public async Task<Session> GetSession(string userId)
        {
            var result = await _userPeerIndex.TryGet(userId);

            if (result.Success)
            {
                return await GetSession(result.Value);
            }
            else
            {
                return null;
            }
        }

        public async Task<PlatformId> GetPlatformId(string userId)
        {
            var session = await GetSession(userId);

            if (session != null)
            {
                return session.platformId;
            }

            return new PlatformId { Platform = "unknown", OnlineId = "" };
        }

        public Task<Session> GetSession(PlatformId id)
        {
            return GetSession(id.ToString());
        }

        public Task<Session> GetSession(IScenePeerClient peer)
        {
            return GetSession(peer.Id);
        }

        public async Task<Session> GetSession(long peerId)
        {
            var result = await _peerUserIndex.TryGet(peerId.ToString());
            if (result.Success)
            {

                return result.Value;
            }
            else
            {
                return null;
            }
        }
    }
}
