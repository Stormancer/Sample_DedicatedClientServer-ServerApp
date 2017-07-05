using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Plugins.Steam.Models
{
    public class SteamPlayerSummary
    {
        public string avatar { get; set; }
        public string avatarfull { get; set; }
        public string avatarmedium { get; set; }
        public int communityvisibilitystate { get; set; }
        public int lastlogoff { get; set; }
        public int loccityid { get; set; }
        public string loccountrycode { get; set; }
        public string locstatecode { get; set; }
        public string personaname { get; set; }
        public int personastate { get; set; }
        public ulong primaryclanid { get; set; }
        public int profilestate { get; set; }
        public string profileurl { get; set; }
        public string realname { get; set; }
        public ulong steamid { get; set; }
        public int timecreate { get; set; }
    }

    public class SteamPlayerSummariesResponse
    {
        public SteamPlayerSummaries response { get; set; }
    }

    public class SteamPlayerSummaries
    {
        public List<SteamPlayerSummary> players { get; set; }
    }
}
