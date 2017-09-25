using MsgPack.Serialization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Plugins.GameSession.Models
{
    [MessagePackEnum(SerializationMethod = EnumSerializationMethod.ByUnderlyingValue)]
    public enum ShutdownMode
    {
        NoPlayerLeft,
        SceneShutdown
    }

    public class ShutdownModeParameters
    {
        [MessagePackMember(0)]
        public ShutdownMode shutdownMode { get; set; }
        [MessagePackMember(1)]
        public int keepSceneAliveFor { get; set; }

    }
}
