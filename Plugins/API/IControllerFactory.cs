using Stormancer.Core;
using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Reflection;
using Stormancer.Plugins;
using Stormancer;
using System.Linq.Expressions;
using Stormancer.Diagnostics;

namespace Server.Plugins.API
{
    public interface IControllerFactory
    {
        void RegisterControllers();
    }

    public class ControllerFactory<T> : IControllerFactory where T : ControllerBase
    {
        private ISceneHost _scene;

        public ControllerFactory(ISceneHost scene)
        {
            _scene = scene;

        }

        private async Task ExecuteRpcAction(RequestContext<IScenePeerClient> ctx, Func<T, RequestContext<IScenePeerClient>, Task> action, string route)
        {
            using (var scope = _scene.DependencyResolver.CreateChild("request"))
            {
                var controller = scope.Resolve<T>();
                try
                {
                    if (controller == null)
                    {
                        throw new InvalidOperationException("The controller could not be found. Make sure it has been properly registered in the dependency manager.");
                    }
                    controller.Request = ctx;

                    await action(controller, ctx);
                }
                catch (Exception ex)
                {
                    if (controller == null || !await controller.HandleException(new ApiExceptionContext(route, ex, ctx)))
                    {
                        scope.Resolve<ILogger>().Log(LogLevel.Error, route, $"An exception occurred while executing action '{route}' in controller '{typeof(T).Name}'.", ex);
                        throw;
                    }
                }
            }
        }

        private async Task ExecuteRouteAction(Packet<IScenePeerClient> packet, Func<T, Packet<IScenePeerClient>, Task> action, string route)
        {
            using (var scope = _scene.DependencyResolver.CreateChild("request"))
            {
                var controller = scope.Resolve<T>();
                //controller.Request = ctx;

                try
                {
                    if (controller == null)
                    {
                        throw new InvalidOperationException("The controller could not be found. Make sure it has been properly registered in the dependency manager.");
                    }
                    await action(controller, packet);
                }
                catch (Exception ex)
                {
                    if (controller == null || !await controller.HandleException(new ApiExceptionContext(route, ex, packet)))
                    {
                        scope.Resolve<ILogger>().Log(LogLevel.Error, route, $"An exception occurred while executing action '{route}' in controller '{typeof(T).Name}'.", ex);
                        throw;
                    }
                }
            }
        }

        public void RegisterControllers()
        {
            var type = typeof(T);

            foreach (var method in type.GetMethods())
            {
                var procedureName = GetProcedureName(type, method);
                if (IsRawAction(method))
                {

                    var ctxParam = Expression.Parameter(typeof(RequestContext<IScenePeerClient>), "ctx");
                    var controllerParam = Expression.Parameter(typeof(T), "controller");
                    var callExpr = Expression.Call(controllerParam, method, ctxParam);

                    var action = Expression.Lambda<Func<T, RequestContext<IScenePeerClient>, Task>>(callExpr, controllerParam, ctxParam).Compile();
                    _scene.AddProcedure(procedureName, ctx => ExecuteRpcAction(ctx, action, procedureName));
                }
                else if (IsRawRoute(method))
                {
                    var ctxParam = Expression.Parameter(typeof(Packet<IScenePeerClient>), "ctx");
                    var controllerParam = Expression.Parameter(typeof(T), "controller");
                    var callExpr = Expression.Call(controllerParam, method, ctxParam);

                    var action = Expression.Lambda<Func<T, Packet<IScenePeerClient>, Task>>(callExpr, controllerParam, ctxParam).Compile();
                    _scene.AddRoute(procedureName, packet => ExecuteRouteAction(packet, action, procedureName), _ => _);
                }
            }

        }

        private bool IsRawAction(MethodInfo method)
        {
            var parameters = method.GetParameters();
            if (parameters.Length != 1)
            {
                return false;
            }

            return method.ReturnType.IsAssignableFrom(typeof(Task)) && parameters[0].ParameterType == typeof(RequestContext<IScenePeerClient>);
        }
        private bool IsRawRoute(MethodInfo method)
        {
            var parameters = method.GetParameters();
            if (parameters.Length != 1)
            {
                return false;
            }
            return method.ReturnType == typeof(Task) && parameters[0].ParameterType == typeof(Packet<IScenePeerClient>);
        }

        private string GetProcedureName(Type controller, MethodInfo method)
        {
            var root = controller.Name.ToLowerInvariant();
            if (root.EndsWith("controller"))
            {
                root = root.Substring(0, root.Length - "Controller".Length);
            }
            return (root + "." + method.Name).ToLowerInvariant();
        }
    }
}