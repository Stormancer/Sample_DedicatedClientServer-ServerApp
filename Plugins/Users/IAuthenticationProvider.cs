using System.Collections.Generic;
using System.Threading.Tasks;
using Stormancer;
using Stormancer.Core;

namespace Server.Users
{
    public interface IAuthenticationProvider
    {
            
        void AddMetadata(Dictionary<string, string> result);

        void Initialize(ISceneHost scene);

        Task<AuthenticationResult> Authenticate(Dictionary<string, string> authenticationCtx, IUserService userService);

       

    }
}