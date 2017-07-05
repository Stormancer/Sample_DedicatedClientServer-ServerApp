using MsgPack.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Plugins.GameSession
{
    public class PlayerUpdate
    {
        [MessagePackMember(0)]
        public string UserId { get; set; }

        [MessagePackMember(1)]
        public byte Status { get; set; }

        [MessagePackMember(2)]
        public string Data { get; set; }
    }
}
