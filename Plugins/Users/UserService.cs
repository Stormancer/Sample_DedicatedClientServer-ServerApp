using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;
using Stormancer;
using Stormancer.Server.Components;
using Stormancer.Diagnostics;
using Nest;
using Server.Plugins.Configuration;

namespace Server.Users
{
    class UserService : IUserService
    {
        private readonly Database.IESClientFactory _clientFactory;
        private string _indexName;
        private readonly ILogger _logger;
        private readonly Lazy<IEnumerable<IUserEventHandler>> _eventHandlers;

        private static bool _mappingChecked = false;
        private static AsyncLock _mappingCheckedLock = new AsyncLock();
        private async Task CreateUserMapping()
        {

            await (await Client()).MapAsync<User>(m => m
                .DynamicTemplates(templates => templates
                    .DynamicTemplate("auth", t => t
                         .PathMatch("auth.*")
                         .MatchMappingType("string")
                         .Mapping(ma => ma.String(s => s.Index(Nest.FieldIndexOption.NotAnalyzed)))
                        )
                    .DynamicTemplate("data", t => t
                         .PathMatch("userData.*")
                         .MatchMappingType("string")
                         .Mapping(ma => ma.String(s => s.Index(Nest.FieldIndexOption.NotAnalyzed)))
                        )
                     )
                 );
        }


        public UserService(UserManagementConfig config, Database.IESClientFactory clientFactory, IConfiguration configuration, ILogger logger, Lazy<IEnumerable<IUserEventHandler>> eventHandlers)
        {
            configuration.SettingsChanged += OnSettingsChanged;
            OnSettingsChanged(configuration, configuration.Settings);
           
            _eventHandlers = eventHandlers;
            _logger = logger;
            //_logger.Log(LogLevel.Trace, "users", $"Using index {_indexName}", new { index = _indexName });

            _clientFactory = clientFactory;
        }

        private void OnSettingsChanged(object sender, dynamic settings)
        {
            _indexName = (string)(settings.index);
        }

        private async Task<Nest.IElasticClient> Client()
        {
            var client = await _clientFactory.CreateClient(_indexName);
            if (!_mappingChecked)
            {
                using (await _mappingCheckedLock.LockAsync())
                {
                    if (!_mappingChecked)
                    {
                        _mappingChecked = true;
                        await CreateUserMapping();
                    }
                }
            }
            return client;
        }
        public async Task<User> AddAuthentication(User user, string provider, JObject authData, string cacheId)
        {
            var c = await Client();

            user.Auth[provider] = authData;

            await c.IndexAsync(user);
            await c.IndexAsync<AuthenticationClaim>(new AuthenticationClaim { Id = provider + "_" + cacheId, UserId = user.Id });
            return user;
        }

        public async Task<User> CreateUser(string id, JObject userData)
        {

            var user = new User() { Id = id, UserData = userData,  };
            var esClient = await Client();
            await esClient.IndexAsync(user);

            return user;
        }


        public async Task<User> GetUserByClaim(string provider, string claimPath, string login)
        {
            var c = await Client();
            var cacheId = provider + "_" + login;
            var claim = await c.GetAsync<AuthenticationClaim>(cacheId);
            if (claim.Found)
            {
                var r = await c.GetAsync<User>(claim.Source.UserId);
                if (r.Found)
                {
                    return r.Source;
                }
                else
                {
                    return null;
                }
            }
            else
            {
                var r = await c.SearchAsync<User>(sd => sd.Query(qd => qd.Term("auth." + provider + "." + claimPath, login)));



                User user;
                if (r.Hits.Count() > 1)
                {
                    user = await MergeUsers(r.Hits.Select(h => h.Source));
                }
                else
                {
                    var h = r.Hits.FirstOrDefault();


                    if (h != null)
                    {

                        user = h.Source;
                    }
                    else
                    {
                        return null;
                    }
                }

                await c.IndexAsync<AuthenticationClaim>(new AuthenticationClaim { Id = cacheId, UserId = user.Id });
                return user;
            }

        }

        private async Task<User> MergeUsers(IEnumerable<User> users)
        {
            var handlers = _eventHandlers.Value;
            foreach (var handler in handlers)
            {
                await handler.OnMergingUsers(users);
            }

            var sortedUsers = users.OrderBy(u => u.CreatedOn).ToList();
            var mainUser = sortedUsers.First();

            var data = new Dictionary<IUserEventHandler, object>();
            foreach (var handler in handlers)
            {
                data[handler] = await handler.OnMergedUsers(sortedUsers.Skip(1), mainUser);
            }

            var c = await Client();
            _logger.Log(Stormancer.Diagnostics.LogLevel.Info, "users", "Merging users.", new { deleting = sortedUsers.Skip(1), into = mainUser });
            await c.BulkAsync(desc =>
            {
                desc = desc.DeleteMany<User>(sortedUsers.Skip(1).Select(u => u.Id))
                           .Index<User>(i => i.Document(mainUser));
                foreach (var handler in handlers)
                {
                    desc = handler.OnBuildMergeQuery(sortedUsers.Skip(1), mainUser, data[handler], desc);
                }
                return desc;
            });
            return mainUser;
        }

        public async Task<User> GetUser(string uid)
        {
            var c = await Client();
            var r = await c.GetAsync<User>(uid);
            if (r.Source != null)
            {
                return r.Source;
            }
            else
            {
                return null;
            }
        }

        public async Task UpdateUserData<T>(string uid, T data)
        {
            var user = await GetUser(uid);
            if (user == null)
            {
                throw new InvalidOperationException("User not found.");
            }
            else
            {
                user.UserData = JObject.FromObject(data);
                await (await Client()).IndexAsync(user);
            }
        }

        public Task LoginEventOccured(User user, IScenePeerClient peer)
        {
            return Task.FromResult(true);
        }

        public Task LogoutEventOccured(User user, long peerId)
        {
           
            return Task.FromResult(true);
        }

        public async Task<IEnumerable<User>> Query(string query, int take, int skip)
        {
            var c = await Client();

            var result = await c.SearchAsync<User>(s => s.Query(q => q.QueryString(qs => qs.Query(query))).Size(take).Skip(skip));

            return result.Documents;
        }

        public async Task UpdateCommunicationChannel(string userId, string channel, JObject data)
        {
            var user = await GetUser(userId);

            if (user == null)
            {
                throw new InvalidOperationException("User not found.");
            }
            else
            {
                user.Channels[channel] = JObject.FromObject(data);
                await (await Client()).IndexAsync(user);
            }

        }

        public async Task Delete(string id)
        {
            var user = await GetUser(id);
            if(user == null)
            {
                throw new InvalidOperationException("User not found");
            }

            var c = await Client();

            var response = await c.DeleteAsync<User>(user.Id);

            if(!response.IsValid)
            {
                throw new InvalidOperationException("DB error : " + response.ServerError.ToString());
            }
        }
    }
}
