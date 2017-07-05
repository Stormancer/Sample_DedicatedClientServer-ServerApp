using Stormancer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Users
{
    public interface IUserSessions
    {
        /// <summary>
        /// Gets the identity of a connected peer.
        /// </summary>
        /// <param name="peer"></param>
        /// <returns>An user instance, or null if the peer isn't authenticated.</returns>
        Task<User> GetUser(IScenePeerClient peer);
        /// <summary>
        /// Gets the peer that has been authenticated with the provided user id.
        /// </summary>
        /// <param name="userId"></param>
        /// <returns>A peer instance of null if no peer is currently authenticated with this identity.</returns>
        Task<IScenePeerClient> GetPeer(string userId);
        Task UpdateUserData<T>(IScenePeerClient peer, T data);
        Task Login(IScenePeerClient peer, User user,PlatformId platformId);
        Task<bool> IsAuthenticated(IScenePeerClient peer);
        Task LogOut(IScenePeerClient peer);

        Task<PlatformId> GetPlatformId(string userId);

        Task<Session> GetSession(string userId);

        Task<Session> GetSession(IScenePeerClient peer);

        Task<Session> GetSession(PlatformId platformId);

    }

    public struct PlatformId
    {
        public override string ToString()
        {
            return Platform + ":" + OnlineId;
        }
        public string Platform { get; set; }
        public string OnlineId { get; set; }
    }
}
