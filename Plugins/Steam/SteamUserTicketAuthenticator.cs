using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Server.Plugins.Steam;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace Server.Users
{
    public class SteamUserTicketAuthenticator : ISteamUserTicketAuthenticator
    {
        private readonly ISteamService _steamService;

        public SteamUserTicketAuthenticator(ISteamService steamService)
        {
            _steamService = steamService;
        }

        public Task<ulong?> AuthenticateUserTicket(string ticket)
        {
            return _steamService.AuthenticateUserTicket(ticket);
        }
    }
}
