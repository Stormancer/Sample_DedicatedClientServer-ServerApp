using Server.Users;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Nest;
using Stormancer;

namespace Server.Users
{
    public interface IUserEventHandler
    {

        Task OnMergingUsers(IEnumerable<Server.Users.User> users);

        Task<Object> OnMergedUsers(IEnumerable<Server.Users.User> enumerable, Server.Users.User mainUser);
        BulkDescriptor OnBuildMergeQuery(IEnumerable<Server.Users.User> enumerable, Server.Users.User mainUser, object data, BulkDescriptor desc);
    }
}
