using Server.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer
{
    public static class UserExtensions
    {
        public static ulong? GetSteamId(this User user)
        {
            var steamId = user.UserData["steamid"].ToObject<string>();
            if (steamId == null)
            {
                return null;
            }
            else
            {
                return ulong.Parse(steamId);
            }
        }
    }
}
