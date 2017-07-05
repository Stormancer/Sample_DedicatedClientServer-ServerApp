using Stormancer;
using Stormancer.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Stormancer.Plugins
{
    public static class ControllerHelper
    {
        public static Func<RequestContext<IScenePeerClient>, Task> ToAction<TData, TResult>(Func<TData, Task<TResult>> typedAction)
        {
            return async (RequestContext<IScenePeerClient> request) =>
            {
                request.SendValue(await typedAction(request.ReadObject<TData>()));
            };
        }

        public static Func<RequestContext<IScenePeerClient>, Task> ToAction<TData>(Func<TData, Task> typedAction)
        {
            return (RequestContext<IScenePeerClient> request) =>
            {
                return typedAction(request.ReadObject<TData>());
            };
        }

        public static Func<RequestContext<IScenePeerClient>, Task> ToActionWithUserData<TUserData, TData, TResult>(Func<TUserData, TData, Task<TResult>> typedAction)
        {
            return async (RequestContext<IScenePeerClient> request) =>
            {
                var userData = request.RemotePeer.GetUserData<TUserData>();

                request.SendValue(await typedAction(userData, request.ReadObject<TData>()));
            };
        }

        public static Func<RequestContext<IScenePeerClient>, Task> ToActionWithUserData<TUserData, TData>(Func<TUserData, TData, Task> typedAction)
        {
            return (RequestContext<IScenePeerClient> request) =>
            {
                var userData = request.RemotePeer.GetUserData<TUserData>();

                return typedAction(userData, request.ReadObject<TData>());
            };
        }

        public static Func<RequestContext<IScenePeerClient>, Task> ToActionWithUserData<TUserData, TResult>(Func<TUserData, Task<TResult>> typedAction)
        {
            return async (RequestContext<IScenePeerClient> request) =>
            {
                var logger = request.RemotePeer.Host.DependencyResolver.Resolve<Diagnostics.ILogger>();

                var userData = request.RemotePeer.GetUserData<TUserData>();

                var result = await typedAction(userData);

                request.SendValue(result);

            };
        }

        public static Func<RequestContext<IScenePeerClient>, Task> ToActionWithUserData<TUserData>(Func<TUserData, Task> typedAction)
        {
            return (RequestContext<IScenePeerClient> request) =>
            {
                var userData = request.RemotePeer.GetUserData<TUserData>();

                return typedAction(userData);
            };
        }
    }
}
