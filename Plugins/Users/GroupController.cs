using Stormancer;
using Stormancer.Plugins;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reactive.Linq;
using Server.Plugins.Database;
using Stormancer.Diagnostics;
using Server.Plugins.Configuration;
using Stormancer.Core;

namespace Server.Users
{
    internal class UserToGroupIndex : InMemoryIndex< UserGroupData> { }
    internal class GroupsIndex : InMemoryIndex< Group> { }

    public struct UserGroupData
    {
        public UserGroupData(string groupId, GroupMemberState pendingInvitation)
        {
            GroupId = groupId;
            State = pendingInvitation;
        }
        public string GroupId { get; private set; }
        public GroupMemberState State { get; private set; }

        public UserGroupData UpdateState(GroupMemberState newState)
        {
            return new UserGroupData(GroupId, newState);
        }
    }

    class GroupController : Plugins.API.ControllerBase
    {
   
        private readonly IUserSessions _users;
        private readonly UserToGroupIndex _userToGroupIndex;
        private readonly GroupsIndex _groupsIndex;
        private readonly ILogger _logger;
        private readonly IConfiguration _config;


        public GroupController(
            IUserSessions users,
            UserToGroupIndex userToGroupIndex,
            GroupsIndex groupsIndex,
            ILogger logger,
            IConfiguration config,
            IActionStore actionStore)
        {
            _users = users;
            _logger = logger;
            _userToGroupIndex = userToGroupIndex;
            _groupsIndex = groupsIndex;
            _config = config;
        }

        private int MaxGroupSize
        {
            get
            {
                return 2;//TODO: Refactor to use configuration settings.
            }
        }
        private int MaxInvitationUserDataLength
        {
            get
            {
                return 1024;//TODO: Refactor to use configuration settings.
            }
        }
        private int MaxGroupUserDataLength
        {
            get
            {
                return 1024;//TODO: Refactor to use configuration settings.
            }
        }

        #region Actions
        /// <summary>
        /// Invite an user in the group of the connected player
        /// </summary>
        /// <param name="ctx"></param>
        /// <returns></returns>
        public async Task Invite(RequestContext<IScenePeerClient> ctx)
        {
            var currentUser = await GetUser();
            var targetUserId = ctx.ReadObject<string>();
            int userDataLength = (int)(ctx.InputStream.Length - ctx.InputStream.Position);
            if (userDataLength > MaxInvitationUserDataLength)
            {
                throw new ClientException($"User data too big ({userDataLength} bytes). Maximum : {MaxInvitationUserDataLength} bytes");
            }
            var userData = new byte[userDataLength];
            ctx.InputStream.Read(userData, 0, userDataLength);
            //Check if user is connected
            var targetPeer = await _users.GetPeer(targetUserId);
            if (targetPeer == null)
            {
                throw new ClientException("The target user is not connected.");
            }
            _logger.Log(LogLevel.Trace, "group.invite", $"'{currentUser.Id}' inviting '{targetUserId}'", new { user = currentUser.Id, target = targetUserId });
            //Create a group whose leader is currentUser
            var group = Group.CreateGroup(currentUser.Id);
            await _groupsIndex.TryAdd(group.Id, group);

            //Get the current group of current user, and set it to the just created group if it doesn't exist.
            var groupResult = await _userToGroupIndex.GetOrAdd(currentUser.Id, new UserGroupData(group.Id, GroupMemberState.InGroup));
            if (groupResult.Value.State != GroupMemberState.InGroup)
            {
                throw new ClientException("You can't invite players when you have a pending invitation running yourself.");
            }
            //If the group of the current user is not the group that we just created, we are in one of these situations:
            // * The current user is already in a group of 1 or several players, in this case this group is the one we should select.
            // * The current user is associated in a way or another to a an invalid group : We should update the link with the new group.
            if (groupResult.Value.GroupId != group.Id)
            {

                var r = await _groupsIndex.TryGet(groupResult.Value.GroupId);
                if (r.Success)//The group that existed prior to the invitation process started is valid. We use it instead of the one we just created.
                {

                    await _groupsIndex.TryRemove(group.Id);
                    group = r.Value;
                }
                else//The group associated with the current player doesn't exist. We should replace the player-group association.
                {
                    if (!(await _userToGroupIndex.TryUpdate(currentUser.Id, new UserGroupData(group.Id, GroupMemberState.InGroup), groupResult.Version)).Success)
                    {
                        throw new ClientException("An error occured. Please retry.");
                    }
                }
            }



            //Set the target player in pending invitation state
            var targetResult = await _userToGroupIndex.GetOrAdd(targetUserId, new UserGroupData(group.Id, GroupMemberState.PendingInvitation));
            while (targetResult.Value.GroupId != group.Id)
            {
                // The user already has an userToGroupIndex
                // * She has a pending invitation
                // * The userToGroupIndex is unsynchronized, the linked group doesn't exist.
                // * She is already in a group
                //     * She is alone in her group. In this case, the former group must be cleant
                if (targetResult.Value.State == GroupMemberState.PendingInvitation)
                {
                    throw new ClientException("The player has already a pending invitation.");
                }
                else
                {
                    var targetGroup = await _groupsIndex.TryGet(targetResult.Value.GroupId);// Find the current group of the invited player

                    if (!targetGroup.Success)//The group doesn't exist. The userToGroupIndex must be cleant.
                    {
                        await _userToGroupIndex.TryRemove(targetUserId);
                        targetResult = await _userToGroupIndex.GetOrAdd(targetUserId, new UserGroupData(group.Id, GroupMemberState.PendingInvitation));
                        continue;
                    }
                    else if (targetGroup.Value.Members.Count == 1)//Alone in a group. Clean it!
                    {
                        await _groupsIndex.TryRemove(targetResult.Value.GroupId);
                        await _userToGroupIndex.TryRemove(targetUserId);
                        targetResult = await _userToGroupIndex.GetOrAdd(targetUserId, new UserGroupData(group.Id, GroupMemberState.PendingInvitation));
                        continue;
                    }
                    else//Grouped with other players. Failure!
                    {
                        throw new ClientException("The player is already in a group.");
                    }
                }
            }

            //Try to add the invitation to the group, and check if the group is not full.
            group = await AddMemberToGroup(group.Id, targetUserId);
            var invitationAccepted = false;
            try
            {
                ////Broadcast to all group member that an invitation is pending.
                //await BroadcastToGroupMembers(group, "group.members.invitationPending", s =>
                //{
                //    using (var writer = new System.IO.BinaryWriter(s, Encoding.UTF8, true))
                //    {
                //        writer.Write(targetUserId);
                //        writer.Write(userData);
                //    }
                //});
                var tcs = new TaskCompletionSource<bool>();
                var disposable = targetPeer.Rpc("group.invite.accept", s =>
                {
                    ctx.InputStream.CopyTo(s);
                }, Stormancer.Core.PacketPriority.MEDIUM_PRIORITY).Subscribe(p =>
                {
                    tcs.TrySetResult(p.Stream.ReadByte() != 0);
                });
                invitationAccepted = await tcs.Task;

            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Error, "groups.invite", $"An error occured while inviting '{targetUserId}' to group '{group.Id}'", ex);

            }
            finally
            {
                await BroadcastToGroupMembers(group, "group.members.invitationComplete", s =>
                {
                    using (var writer = new System.IO.BinaryWriter(s, Encoding.UTF8, true))
                    {
                        writer.Write(targetUserId);
                        writer.Write(invitationAccepted);
                    }
                });
            }
            if (!invitationAccepted)//Refused invitation or error
            {
                await RemoveMemberFromGroup(group.Id, targetUserId);
                await _userToGroupIndex.TryRemove(targetUserId);
                throw new ClientException("The player refused the invitation.");
            }
            else
            {
                await _userToGroupIndex.UpdateWithRetries(targetUserId, d => d.UpdateState(GroupMemberState.InGroup));
                var r = await _groupsIndex.UpdateWithRetries(group.Id, g => g.AcceptInvitation(targetUserId));
                group = r.Value;
               
            }
             await BroadCastGroupUpdate(group);

        }


        public Task Exclude(RequestContext<IScenePeerClient> ctx)
        {
            throw new NotImplementedException();
            //TODO: Exclusion requires some kind of player validation where no system is perfect.
        }

        public async Task Leave(RequestContext<IScenePeerClient> ctx)
        {
            var currentUser = await GetUser();

            var group = await GetGroup(currentUser.Id);


            if (group.Members.Count == 1)//Cannot leave group when alone in it.
            {
                return;
            }

            if (group.Leader == currentUser.Id)
            {
                var promotionCandidate = group.Members.Values.Where(m => m.Id != currentUser.Id && m.State == GroupMemberState.InGroup).OrderBy(m => m.Created).FirstOrDefault();
                if (promotionCandidate.Id == null)//No candidate for leadership. Disband group.
                {

                }
            }
        }

        public async Task Promote(RequestContext<IScenePeerClient> ctx)
        {
            var currentUser = await GetUser();
            var targetUserId = ctx.ReadObject<string>();
            var group = await GetGroup(currentUser.Id);

            if (group.Leader != currentUser.Id)
            {
                throw new ClientException($"Only the leader can promote another player as leader.");
            }

            var r = await _groupsIndex.UpdateWithRetries(group.Id, g => g.PromoteAsLeader(targetUserId));
            if (!r.Success)
            {
                throw new ClientException("Failed to promote player.");
            }
            await BroadCastGroupUpdate(r.Value);
        }

        public async Task UpdateGroupData(RequestContext<IScenePeerClient> ctx)
        {
            var currentUser = await GetUser();

            var group = await GetGroup(currentUser.Id);

            if (group.Leader != currentUser.Id)
            {
                throw new ClientException($"Only the leader can update group data.");
            }
            int userDataLength = (int)(ctx.InputStream.Length - ctx.InputStream.Position);
            if (userDataLength > MaxGroupUserDataLength)
            {
                throw new ClientException($"User data too big ({userDataLength} bytes). Maximum : {MaxGroupUserDataLength} bytes");
            }
            byte[] data = new byte[userDataLength];

            var r = await _groupsIndex.UpdateWithRetries(group.Id, g => g.UpdateUserData(data));
            if (!r.Success)
            {
                throw new ClientException("Failed to update group user data.");
            }
            await BroadCastGroupUpdate(r.Value);
        }

        /// <summary>
        /// Get the group of the current user.
        /// </summary>
        /// <returns></returns>
        public async Task GetGroup(RequestContext<IScenePeerClient> ctx)
        {
            var currentUser = await GetUser();

            var group = await GetGroup(currentUser.Id);


            this.Request.SendValue(group);
        }
        #endregion

        private async Task<Group> GetGroup(string userId)
        {
            var r = await _userToGroupIndex.GetOrAdd(userId, new UserGroupData(Guid.NewGuid().ToString(), GroupMemberState.InGroup));

            if (r.Success)
            {
                var r2 = await _groupsIndex.TryGet(r.Value.GroupId);
                if (r2.Success)
                {
                    if (r2.Value.IsInGroup(userId))
                    {
                        if (r2.Value.Members.Count == 1 && r2.Value.Leader != userId)//Alone in her group, but not leader=>promoted to leadership.
                        {

                            await _groupsIndex.UpdateWithRetries(r2.Value.Id, g => g.PromoteAsLeader(userId));


                        }
                        return r2.Value;

                    }

                }
                //The group id in _userToGroupIndex is invalid. Clean the index, then proceed as if the player hadn't any group.
                await _userToGroupIndex.TryRemove(userId);

            }

            var groupCreated = false;
            Group group = default(Group);
            while (!groupCreated)
            {
                group = Group.CreateGroup(userId);
                groupCreated = await _groupsIndex.TryAdd(r.Value.GroupId, group);

            }

            return group;

        }
        private async Task<User> GetUser()
        {
            var currentUser = await _users.GetUser(Request.RemotePeer);

            if (currentUser == null)
            {
                throw new ClientException("You must be authenticated to perform this operation.");

            }
            return currentUser;
        }

        private Task BroadCastGroupUpdate(Group group)
        {
            return BroadcastToGroupMembers(group, "group.update", group, PacketReliability.RELIABLE_SEQUENCED);
        }
        private async Task BroadcastToGroupMembers<T>(Group group, string route, T data, PacketReliability reliability)
        {
            var peerTasks = new List<Task>();
            foreach (var memberId in group.Members.Keys)
            {
                peerTasks.Add(_users.GetPeer(memberId).ContinueWith(t =>
                    t.Result.Send(route, data, Stormancer.Core.PacketPriority.MEDIUM_PRIORITY, reliability)
                ));
            }
            await Task.WhenAll(peerTasks);

        }
        private async Task BroadcastToGroupMembers(Group group, string route, Action<System.IO.Stream> writer)
        {
            var peerTasks = new List<Task>();
            foreach (var memberId in group.Members.Keys)
            {
                peerTasks.Add(_users.GetPeer(memberId).ContinueWith(t =>
                    t.Result.Send(route, writer, Stormancer.Core.PacketPriority.MEDIUM_PRIORITY, Stormancer.Core.PacketReliability.RELIABLE)
                ));
            }
            await Task.WhenAll(peerTasks);

        }

        private async Task<Group> AddMemberToGroup(string groupId, string memberId)
        {
            var group = await RunGroupUpdateWithRetries(groupId, g =>
            {
                if (g.Members.Keys.Count >= this.MaxGroupSize)
                {
                    throw new ClientException("Invitation failed: group full.");
                }
                return g.AddMemberInvitation(memberId);
            });
            await BroadCastGroupUpdate(group);
            return group;

        }
        private Task RemoveMemberFromGroup(string groupId, string memberId)
        {
            return RunGroupUpdateWithRetries(groupId, g => g.RemoveMember(memberId));
        }
        private async Task<Group> RunGroupUpdateWithRetries(string groupId, Func<Group, Group> mutator)
        {
            var r = await _groupsIndex.UpdateWithRetries(groupId, mutator);
            return r.Value;
        }
    }

    internal class Group
    {
        public Group(string groupId, IEnumerable<GroupMember> members, string leader, byte[] userData)
        {
            if (!members.Select(m => m.Id).Contains(leader))
            {
                throw new ArgumentException("The group leader must be a group member.");
            }
            if (members.Select(m => m.Id).Distinct().Count() != members.Count())
            {
                throw new ArgumentException("Duplicate members in the group.");
            }
            Members = new Dictionary<string, GroupMember>();

            foreach (var member in members)
            {
                Members.Add(member.Id, member);
            }
            Leader = leader;
            Id = groupId;
            UserData = userData;
        }
        public static Group CreateGroup(string leader)
        {
            return new Group(Guid.NewGuid().ToString(), new[] { new GroupMember(leader, GroupMemberState.InGroup, DateTime.UtcNow) }, leader, new byte[0]);
        }
        public Group AddMemberInvitation(string member)
        {
            if (IsInGroup(member))
            {
                throw new ArgumentException($"Failed to invite '{member}': already in the group.");
            }
            return new Group(this.Id, this.Members.Values.Concat(new[] { new GroupMember(member, GroupMemberState.PendingInvitation, DateTime.UtcNow) }), this.Leader, this.UserData);
        }
        public Group AcceptInvitation(string member)
        {
            if (!IsInGroup(member))
            {
                throw new ArgumentException($"Failed to accept invitation: '{member}' is not in the group.");
            }
            var members = new Dictionary<string, GroupMember>(this.Members);
            members[member] = new GroupMember(member, GroupMemberState.InGroup, members[member].Created);
            return new Group(this.Id, members.Values, this.Leader, this.UserData);
        }

        public Group RemoveMember(string member)
        {
            if (!IsInGroup(member))
            {
                throw new ArgumentException($"'{member}' failed to leave group: not in the group.");
            }
            if (member == Leader)
            {
                throw new ArgumentException($"'{member} failed to leave group: leader cannot leave the group.");
            }
            return new Group(this.Id, this.Members.Values.Where(g => g.Id != member), Leader, this.UserData);
        }

        public Group PromoteAsLeader(string member)
        {
            if (!IsInGroup(member))
            {
                throw new ArgumentException($"Failed to promote '{member}' as leader : not in the group.");
            }
            if (Members[member].State != GroupMemberState.InGroup)
            {
                throw new ArgumentException($"Failed to promote '{member}' as leader: Pending group invitation not yet accepted.");
            }
            return new Group(this.Id, this.Members.Values, member, this.UserData);
        }

        public bool IsInGroup(string id)
        {
            return Members.ContainsKey(id);
        }

        public Group UpdateUserData(byte[] data)
        {
            return new Group(this.Id, this.Members.Values, this.Leader, data);
        }

        public Dictionary<string, GroupMember> Members { get; set; }

        public string Leader { get; set; }

        public string Id { get; set; }

        public byte[] UserData { get; set; }
    }

    public struct GroupMember
    {
        public GroupMember(string id, GroupMemberState inGroup, DateTime created)
        {
            Id = id;
            State = inGroup;
            Created = created;
        }

        /// <summary>
        /// Id of the group member
        /// </summary>
        public string Id { get; set; }

        /// <summary>
        /// True if the member is effectively in the group, false if it's only a pending invitation.
        /// </summary>
        public GroupMemberState State { get; set; }

        public DateTime Created { get; set; }
    }

    public enum GroupMemberState
    {
        PendingInvitation,
        InGroup
    }
}
