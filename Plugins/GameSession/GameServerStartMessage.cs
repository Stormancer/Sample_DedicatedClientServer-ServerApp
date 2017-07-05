using MsgPack.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Plugins.GameSession
{
    public class GameServerStartMessage
    {
        [MessagePackMember(0)]
        public string P2PToken { get; set; }

    }
}
