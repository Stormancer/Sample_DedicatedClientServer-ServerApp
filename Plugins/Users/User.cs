using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json.Linq;

namespace Server.Users
{
    public class User
    {
        public User()
        {
            Auth = new JObject();
            UserData = new JObject();
            CreatedOn = DateTime.UtcNow;
            Channels = new JObject();
        }

        public string Id { get; set; }

    
        public JObject Auth { get; set; }
        public JObject UserData { get; set; } 

        public DateTime CreatedOn { get; set; }

        public DateTime LastLogin { get; set; }

        public JObject Channels { get; set; }
    }

    public class AuthenticationClaim
    {
        public string Id { get; set; }

        public string UserId { get; set; }
    }
}
