using MsgPack.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Users
{
    public class LoginResult
    {
        [MessagePackMember(0)]
        public string ErrorMsg { get; set; } = "";

        [MessagePackMember(1)]
        public bool Success { get; set; }
        
        [MessagePackMember(2)]
        public string UserId { get; set; } = "";

        [MessagePackMember(3)]
        public string Username { get; set; } = "";
    }
}
