using Stormancer;
using Stormancer.Core;
using Stormancer.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Plugins.API
{
    public class ApiExceptionContext
    {
        public ApiExceptionContext(string route, Exception exception, RequestContext<IScenePeerClient> request) : this(route, exception)
        {
            Request = request;
        }

        public  ApiExceptionContext(string route, Exception exception, Packet<IScenePeerClient> packet) : this(route, exception)
        {
            Packet = packet;
        }

        private ApiExceptionContext(string route, Exception exception)
        {
            this.Route = route;
            this.Exception = exception;
        }

        public Exception Exception { get; }
        public string Route { get; }
        public RequestContext<IScenePeerClient> Request { get; }
        public Packet<IScenePeerClient> Packet { get; }

        public bool IsRpc => Request != null;
        public bool IsRoute => Packet != null;
    }
    
}
