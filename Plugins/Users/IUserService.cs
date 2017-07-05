using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Stormancer;

namespace Server.Users
{
    public interface IUserService
    {
        Task<IEnumerable<User>> Query(string query, int take, int skip);

        Task<User> GetUser(string uid);
        Task<User> AddAuthentication(User user,string provider, JObject authData, string cacheId); 
        Task<User> GetUserByClaim(string provider, string claimPath, string login);
        Task<User> CreateUser(string uid, JObject userData);
        Task LoginEventOccured(User user, IScenePeerClient peer);
        Task LogoutEventOccured(User user, long peerId);
        Task UpdateUserData<T>(string uid, T data);

        Task UpdateCommunicationChannel(string userId, string channel, JObject data);
        Task Delete(string id);


    }
}
