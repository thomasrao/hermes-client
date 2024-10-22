using Serilog;
using TwitchChatTTS.Chat.Commands;
using TwitchChatTTS.Chat.Commands.Limits;
using TwitchChatTTS.Chat.Groups;
using TwitchChatTTS.Chat.Groups.Permissions;
using TwitchChatTTS.Chat.Messaging;
using TwitchChatTTS.Twitch.Socket.Messages;

namespace TwitchChatTTS.Twitch.Socket.Handlers
{
    public class ChannelChatMessageHandler : ITwitchSocketHandler
    {
        public string Name => "channel.chat.message";

        private readonly IChatMessageReader _reader;
        private readonly User _user;
        private readonly ICommandManager _commands;
        private readonly IGroupPermissionManager _permissionManager;
        private readonly IUsagePolicy<long> _permissionPolicy;
        private readonly IChatterGroupManager _chatterGroupManager;
        private readonly ILogger _logger;


        public ChannelChatMessageHandler(
            IChatMessageReader reader,
            ICommandManager commands,
            IGroupPermissionManager permissionManager,
            IUsagePolicy<long> permissionPolicy,
            IChatterGroupManager chatterGroupManager,
            User user,
            ILogger logger
        )
        {
            _reader = reader;
            _user = user;
            _commands = commands;
            _permissionManager = permissionManager;
            _permissionPolicy = permissionPolicy;

            _chatterGroupManager = chatterGroupManager;
            _logger = logger;

            _permissionPolicy.Set("everyone", "tts", 100, TimeSpan.FromSeconds(15));
            _permissionPolicy.Set("everyone", "tts.chat.messages.read", 3, TimeSpan.FromMilliseconds(15000));
        }


        public async Task Execute(TwitchWebsocketClient sender, object data)
        {
            if (sender == null)
                return;
            if (data is not ChannelChatMessage message)
                return;

            var chatterId = long.Parse(message.ChatterUserId);
            var chatterLogin = message.ChatterUserLogin;
            var fragments = message.Message.Fragments;
            var groups = GetGroups(message.Badges, chatterId);
            var bits = GetTotalBits(fragments);

            var commandResult = await CheckForChatCommand(message.Message.Text, message, groups);
            if (commandResult != ChatCommandResult.Unknown)
                return;

            string permission = GetPermissionPath(message.ChannelPointsCustomRewardId, bits);
            if (!HasPermission(chatterId, groups, permission))
            {
                _logger.Debug($"Blocked message [chatter: {chatterLogin}][message: {message}]");
                return;
            }

            if (!_permissionPolicy.TryUse(chatterId, groups, permission))
            {
                _logger.Debug($"Chatter has been rate limited from TTS [chatter: {chatterLogin}][chatter id: {chatterId}][message: {message}]");
                return;
            }

            var broadcasterId = long.Parse(message.BroadcasterUserId);
            int priority = _chatterGroupManager.GetPriorityFor(groups);
            await _reader.Read(sender, broadcasterId, chatterId, chatterLogin, message.MessageId, message.Reply, fragments, priority);
        }

        private async Task<ChatCommandResult> CheckForChatCommand(string arguments, ChannelChatMessage message, IEnumerable<string> groups)
        {
            try
            {
                return await _commands.Execute(arguments, message, groups);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed executing a chat command [message: {arguments}][chatter: {message.ChatterUserLogin}][chatter id: {message.ChatterUserId}][message id: {message.MessageId}]");
            }
            return ChatCommandResult.Fail;
        }

        private string GetGroupNameByBadgeName(string badgeName)
        {
            if (badgeName == "subscriber")
                return "subscribers";
            if (badgeName == "moderator")
                return "moderators";
            return badgeName.ToLower();
        }

        private IEnumerable<string> GetGroups(TwitchBadge[] badges, long chatterId)
        {
            var defaultGroups = new string[] { "everyone" };
            var badgesGroups = badges.Select(b => b.SetId).Select(GetGroupNameByBadgeName);
            var customGroups = _chatterGroupManager.GetGroupNamesFor(chatterId);
            return defaultGroups.Union(badgesGroups).Union(customGroups);
        }

        private int GetTotalBits(TwitchChatFragment[] fragments)
        {
            return fragments.Where(f => f.Type == "cheermote" && f.Cheermote != null)
                .Select(f => f.Cheermote!.Bits)
                .Sum();
        }

        private string GetPermissionPath(string? customRewardId, int bits)
        {
            var permissionPath = "tts.chat.messages.read";
            if (!string.IsNullOrWhiteSpace(customRewardId))
                permissionPath = "tts.chat.redemptions.read";
            else if (bits > 0)
                permissionPath = "tts.chat.bits.read";
            return permissionPath;
        }

        private bool HasPermission(long chatterId, IEnumerable<string> groups, string permissionPath)
        {
            return chatterId == _user.OwnerId ? true : _permissionManager.CheckIfAllowed(groups, permissionPath) == true;
        }
    }
}