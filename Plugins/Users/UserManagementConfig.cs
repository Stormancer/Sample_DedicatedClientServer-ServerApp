using Stormancer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Users
{
    public class UserManagementConfig
    {
        public UserManagementConfig()
        {
            AuthenticationProviders = new List<IAuthenticationProvider>();
            UserDataSelector = DefaultUserDataSelector;
        }

        public List<IAuthenticationProvider> AuthenticationProviders { get; private set; }

        public string SceneIdRedirect { get; set; }

        private Func<AuthenticationResult, string> _sceneIdRedirectFactory = null;
        public Func<AuthenticationResult, string> OnRedirect
        {
            get
            {
                return _sceneIdRedirectFactory ?? (authenticationResult => SceneIdRedirect);
            }
            set
            {
                _sceneIdRedirectFactory = value;
            }
        }

        private string DefaultUserDataSelector(AuthenticationResult r)
        {
            return r.AuthenticatedUser.Id;
        }

        public Func<AuthenticationResult, object> UserDataSelector { get; set; }
    }
}
