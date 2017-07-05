using System.Threading.Tasks;

namespace Server.Users
{
    public interface ISteamUserTicketAuthenticator
    {
        Task<ulong?> AuthenticateUserTicket(string ticket);
    }
}