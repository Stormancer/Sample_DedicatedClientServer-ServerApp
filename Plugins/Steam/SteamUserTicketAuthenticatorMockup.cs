using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Users
{
    public class SteamUserTicketAuthenticatorMockup : ISteamUserTicketAuthenticator
    {
        public Task<ulong?> AuthenticateUserTicket(string ticket)
        {
            if(ticket  == "invalid")
            {
                return Task.FromResult<ulong?>(null);
            }
            else
            {
                return Task.FromResult<ulong?>((ulong)(ticket.GetHashCode()));
            }
        }
    }
}
