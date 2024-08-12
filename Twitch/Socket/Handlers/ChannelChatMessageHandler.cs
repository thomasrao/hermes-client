using Serilog;
using TwitchChatTTS.Chat.Commands;
using TwitchChatTTS.Chat.Groups;
using TwitchChatTTS.Chat.Groups.Permissions;
using TwitchChatTTS.Chat.Messaging;
using TwitchChatTTS.Twitch.Socket.Messages;

namespace TwitchChatTTS.Twitch.Socket.Handlers
{
    public class ChannelChatMessageHandler : ITwitchSocketHandler
    {
        public string Name => "channel.chat.message";

        private readonly ChatMessageReader _reader;
        private readonly User _user;
        private readonly ICommandManager _commands;
        private readonly IGroupPermissionManager _permissionManager;
        private readonly IChatterGroupManager _chatterGroupManager;
        private readonly ILogger _logger;


        public ChannelChatMessageHandler(
            ChatMessageReader reader,
            ICommandManager commands,
            IGroupPermissionManager permissionManager,
            IChatterGroupManager chatterGroupManager,
            User user,
            ILogger logger
        )
        {
            _reader = reader;
            _user = user;
            _commands = commands;
            _permissionManager = permissionManager;
            _chatterGroupManager = chatterGroupManager;
            _logger = logger;
        }


        public async Task Execute(TwitchWebsocketClient sender, object data)
        {
            if (sender == null)
                return;
            if (data is not ChannelChatMessage message)
                return;

            var broadcasterId = long.Parse(message.BroadcasterUserId);
            var chatterId = long.Parse(message.ChatterUserId);
            var chatterLogin = message.ChatterUserLogin;
            var messageId = message.MessageId;
            var fragments = message.Message.Fragments;
            var groups = GetGroups(message.Badges, chatterId);
            var bits = GetTotalBits(fragments);

            var commandResult = await CheckForChatCommand(message.Message.Text, message, groups);
            if (commandResult != ChatCommandResult.Unknown)
                return;

            if (!HasPermission(message.ChannelPointsCustomRewardId, chatterId, groups, bits))
            {
                _logger.Debug($"Blocked message by {chatterLogin}: {message}");
                return;
            }

            int priority = _chatterGroupManager.GetPriorityFor(groups);
            await _reader.Read(sender, broadcasterId, chatterId, chatterLogin, messageId, message.Reply, fragments, priority);
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

        private bool HasPermission(string? customRewardId, long chatterId, IEnumerable<string> groups, int bits)
        {
            var permissionPath = "tts.chat.messages.read";
            if (!string.IsNullOrWhiteSpace(customRewardId))
                permissionPath = "tts.chat.redemptions.read";
            else if (bits > 0)
                permissionPath = "tts.chat.bits.read";

            return chatterId == _user.OwnerId ? true : _permissionManager.CheckIfAllowed(groups, permissionPath) == true;
        }
    }
}