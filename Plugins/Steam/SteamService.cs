using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Server.Plugins.Steam.Models;
using Stormancer.Server.Components;
using System.Net.Http;
using System.IO;
using Newtonsoft.Json;
using System.Linq;
using Server.Plugins.Configuration;
using Newtonsoft.Json.Linq;
using Server.Users;

namespace Server.Plugins.Steam
{
    public class SteamService : ISteamService
    {
        private const string ApiRoot = "https://partner.steam-api.com";
        private const string FallbackApiRoot = "https://api.steampowered.com";
        private const string FallbackApiRooWithIp = "https://208.64.202.87";

        private string _apiKey;
        private uint _appId;

        private bool _usemockup;

        
        public SteamService(IConfiguration configuration)
        {
           
            var steamElement = configuration.Settings?.steam;



            ApplyConfig(steamElement);

            configuration.SettingsChanged += (sender, settings) => ApplyConfig(settings?.steam);
        }

        private void ApplyConfig(dynamic steamElement)
        {
            _apiKey = (string)steamElement?.apiKey;

            var dynamicAppId = steamElement?.appId;
            if (dynamicAppId != null)
            {
                _appId = (uint)dynamicAppId;
            }

            var dynamicUseMockup = steamElement?.usemockup;
            if (dynamicUseMockup != null)
            {
                _usemockup = (bool)dynamicUseMockup;
            }
        }

        public async Task<ulong?> AuthenticateUserTicket(string ticket)
        {
            if (_usemockup)
            {
                return (ulong)ticket.GetHashCode();
            }

            const string AuthenticateUri = "ISteamUserAuth/AuthenticateUserTicket/v0001/";

            var querystring = $"?key={_apiKey}"
                + $"&appid={_appId}"
                + $"&ticket={ticket}";

            using (var response = await TryGetAsync(AuthenticateUri + querystring))
            {
                response.EnsureSuccessStatusCode();
                var json = await response.Content.ReadAsStringAsync();
                var steamResponse = JsonConvert.DeserializeObject<SteamAuthenticationResponse>(json);

                if (steamResponse.response.error != null)
                {
                    throw new Exception($"The Steam API failed to authenticate user ticket : {steamResponse.response.error.errorcode} : '{steamResponse.response.error.errordesc}'. AppId : {_appId}");
                }
                else
                {
                    return steamResponse.response.@params.steamid;
                }
            }


        }

        public async Task<string> OpenVACSession(string steamId)
        {
            const string uri = "ICheatReportingService/StartSecureMultiplayerSession/v0001/";
            var p = new Dictionary<string, string> {
                {"key",_apiKey },
                {"appid",_appId.ToString() },
                {"steamid",steamId }
            };
            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(ApiRoot);

                using (var response = await client.PostAsync(uri, new FormUrlEncodedContent(p)))
                {
                    response.EnsureSuccessStatusCode();
                    var json = await response.Content.ReadAsStringAsync();
                    dynamic j = JObject.Parse(json);
                    var success = (bool)j.response.success;
                    var sessionId = (string)j.response.session_id;
                    return sessionId;
                }

            }

        }

        public async Task CloseVACSession(string steamId, string sessionId)
        {
            const string uri = "ICheatReportingService/EndSecureMultiplayerSession/v0001/";
            var p = new Dictionary<string, string> {
                {"key",_apiKey },
                {"appid",_appId.ToString() },
                {"steamid",steamId },
                {"session_id",sessionId }
            };

            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(ApiRoot);

                using (var response = await client.PostAsync(uri, new FormUrlEncodedContent(p)))
                {
                    response.EnsureSuccessStatusCode();
                    var json = await response.Content.ReadAsStringAsync();
                    var j = JObject.Parse(json);

                }

            }
        }

        public async Task<bool> RequestVACStatusForUser(string steamId, string sessionId)
        {
            const string uri = "ICheatReportingService/RequestVacStatusForUser/v0001/";
            var p = new Dictionary<string, string> {
                {"key",_apiKey },
                {"appid",_appId.ToString() },
                {"steamid",steamId },
                {"session_id",sessionId }
            };

            using (var client = new HttpClient())
            {
                client.BaseAddress = new Uri(ApiRoot);

                using (var response = await client.PostAsync(uri, new FormUrlEncodedContent(p)))
                {

                    response.EnsureSuccessStatusCode();
                    var json = await response.Content.ReadAsStringAsync();
                    dynamic j = JObject.Parse(json);
                    var sessionVerified = (bool)j.response.session_verified;
                    var success = (bool)j.response.success;
                    return success && sessionVerified;


                }

            }
        }

        public async Task<Dictionary<ulong, SteamPlayerSummary>> GetPlayerSummaries(IEnumerable<ulong> steamIds)
        {
            if (_usemockup)
            {
                return steamIds.ToDictionary(id => id, id => new SteamPlayerSummary { personaname = "player" + id.ToString(), steamid = id });
            }

            const string GetPlayerSummariesUri = "ISteamUser/GetPlayerSummaries/V0002/";

            var steamIdsWithoutRepeat = steamIds.Distinct().ToList();
            Dictionary<ulong, SteamPlayerSummary> result = new Dictionary<ulong, SteamPlayerSummary>();

            for (var i = 0; i * 100 < steamIdsWithoutRepeat.Count; i++)
            {
                var querystring = $"?key={_apiKey}"
                    + $"&steamids={string.Join(",", steamIdsWithoutRepeat.Skip(100 * i).Take(100).Select(v => v.ToString()))}";

                using (var response = await TryGetAsync(GetPlayerSummariesUri + querystring))
                {

                    response.EnsureSuccessStatusCode();
                    var json = await response.Content.ReadAsStringAsync();
                    var steamResponse = JsonConvert.DeserializeObject<SteamPlayerSummariesResponse>(json);

                    foreach (var summary in steamResponse.response.players)
                    {
                        result.Add(summary.steamid, summary);
                    }
                }
            }

            return result;
        }

        private async Task<HttpResponseMessage> TryGetAsync(string requestUrl)
        {
            using (var client = new HttpClient())
            {
                try
                {
                    return await client.GetAsync(new Uri(new Uri(ApiRoot), requestUrl));
                }
                catch (HttpRequestException)
                {
                    try
                    {
                        return await client.GetAsync(new Uri(new Uri(FallbackApiRoot), requestUrl));
                    }
                    catch (HttpRequestException)
                    {
                        return await client.GetAsync(new Uri(new Uri(FallbackApiRooWithIp), requestUrl));
                    }
                }
            }
        }

        public async Task<SteamPlayerSummary> GetPlayerSummary(ulong steamId)
        {
            return (await GetPlayerSummaries(new[] { steamId }))?[steamId];
        }

       
    }
}