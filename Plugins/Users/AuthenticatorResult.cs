using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Server.Users
{
    public class AuthenticationResult
    {
        private AuthenticationResult()
        {
        }

        public static AuthenticationResult CreateSuccess(User user, PlatformId platformId, Dictionary<string, string> context)
        {
            return new AuthenticationResult { Success = true, AuthenticatedUser = user, PlatformId = platformId, AuthenticationContext = context };
        }

        public static AuthenticationResult CreateFailure(string reason, PlatformId platformId, Dictionary<string, string> context)
        {
            return new AuthenticationResult { Success = false, ReasonMsg = reason, PlatformId = platformId, AuthenticationContext = context };
        }

        public bool Success { get; private set; }

        public string AuthenticatedId
        {
            get
            {
                return AuthenticatedUser?.Id;
            }
        }

        public User AuthenticatedUser { get; private set; }

        public string ReasonMsg { get; private set; }

        public PlatformId PlatformId { get; private set; }

        public string Username
        {
            get
            {
                dynamic userData = AuthenticatedUser?.UserData;
                return (string)userData?.pseudo??"";
            }
        }

        public Dictionary<string, string> AuthenticationContext { get; private set; }
    }
}
