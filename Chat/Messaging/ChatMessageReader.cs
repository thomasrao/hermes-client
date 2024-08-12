using System.Text.RegularExpressions;
using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TwitchChatTTS.Chat.Commands;
using TwitchChatTTS.Chat.Emotes;
using TwitchChatTTS.Chat.Groups;
using TwitchChatTTS.Chat.Groups.Permissions;
using TwitchChatTTS.Chat.Speech;
using TwitchChatTTS.Hermes.Socket;
using TwitchChatTTS.OBS.Socket;
using TwitchChatTTS.Twitch.Socket;
using TwitchChatTTS.Twitch.Socket.Messages;

namespace TwitchChatTTS.Chat.Messaging
{
    public class ChatMessageReader
    {
        public string Name => "channel.chat.message";

        private readonly User _user;
        private readonly TTSPlayer _player;
        private readonly ICommandManager _commands;
        private readonly IGroupPermissionManager _permissionManager;
        private readonly IChatterGroupManager _chatterGroupManager;
        private readonly IEmoteDatabase _emotes;
        private readonly OBSSocketClient _obs;
        private readonly HermesSocketClient _hermes;
        private readonly Configuration _configuration;
        private readonly ILogger _logger;

        private readonly Regex _sfxRegex;


        public ChatMessageReader(
            User user,
            TTSPlayer player,
            ICommandManager commands,
            IGroupPermissionManager permissionManager,
            IChatterGroupManager chatterGroupManager,
            IEmoteDatabase emotes,
            [FromKeyedServices("hermes")] SocketClient<WebSocketMessage> hermes,
            [FromKeyedServices("obs")] SocketClient<WebSocketMessage> obs,
            Configuration configuration,
            ILogger logger
        )
        {
            _user = user;
            _player = player;
            _commands = commands;
            _permissionManager = permissionManager;
            _chatterGroupManager = chatterGroupManager;
            _emotes = emotes;
            _obs = (obs as OBSSocketClient)!;
            _hermes = (hermes as HermesSocketClient)!;
            _configuration = configuration;
            _logger = logger;

            _sfxRegex = new Regex(@"\(([A-Za-z0-9_-]+)\)", RegexOptions.Compiled);
            _logger = logger;
        }

        public async Task Execute(TwitchWebsocketClient sender, ChannelChatMessage message)
        {
            if (_hermes.Connected && !_hermes.Ready)
            {
                _logger.Debug($"TTS is not yet ready. Ignoring chat messages [message id: {message.MessageId}]");
                return; // new MessageResult(MessageStatus.NotReady, -1, -1);
            }
            if (_configuration.Twitch?.TtsWhenOffline != true && !_obs.Streaming)
            {
                _logger.Debug($"OBS is not streaming. Ignoring chat messages [message id: {message.MessageId}]");
                return; // new MessageResult(MessageStatus.NotReady, -1, -1);
            }

            var chatterId = long.Parse(message.ChatterUserId);
            var broadcasterId = long.Parse(message.BroadcasterUserId);
            var messageId = message.MessageId;
            var groups = GetGroups(message, chatterId);
            var commandResult = await CheckForChatCommand(message.Message.Text, message, groups);
            if (commandResult != ChatCommandResult.Unknown)
                return;

            var bits = GetTotalBits(message);
            if (!HasPermission(message, chatterId, groups, bits))
            {
                _logger.Debug($"Blocked message by {message.ChatterUserLogin}: {message.Message.Text}");
                return;
            }

            var emoteUsage = GetEmoteUsage(message);
            var tasks = new List<Task>();
            if (_obs.Streaming)
            {
                if (emoteUsage.NewEmotes.Any())
                    tasks.Add(_hermes.SendEmoteDetails(emoteUsage.NewEmotes));
                if (emoteUsage.EmotesUsed.Any())
                    tasks.Add(_hermes.SendEmoteUsage(message.MessageId, chatterId, emoteUsage.EmotesUsed));
                if (!_user.Chatters.Contains(chatterId))
                {
                    tasks.Add(_hermes.SendChatterDetails(chatterId, message.ChatterUserLogin));
                    _user.Chatters.Add(chatterId);
                }
            }

            if (_user.Raids.TryGetValue(message.BroadcasterUserId, out var raid) && !raid.Chatters.Contains(chatterId))
            {
                _logger.Information($"Potential chat message from raider ignored due to potential raid message spam [chatter: {message.ChatterUserLogin}][chatter id: {message.ChatterUserId}]");
                return;
            }

            var msg = FilterMessage(message);
            int priority = _chatterGroupManager.GetPriorityFor(groups);
            string voiceSelected = GetSelectedVoiceFor(chatterId);
            var messages = GetPartialTTSMessages(msg, voiceSelected).ToList();
            var groupedMessage = new TTSGroupedMessage(broadcasterId, chatterId, messageId, messages, DateTime.UtcNow, priority);
            _player.Add(groupedMessage, groupedMessage.Priority);
            
            if (tasks.Any())
                await Task.WhenAll(tasks);
        }

        private IEnumerable<TTSMessage> HandlePartialMessage(string voice, string message)
        {
            var parts = _sfxRegex.Split(message);

            if (parts.Length == 1)
            {
                return [new TTSMessage()
                {
                    Voice = voice,
                    Message = message,
                }];
            }

            var list = new List<TTSMessage>();
            var sfxMatches = _sfxRegex.Matches(message);
            for (var i = 0; i < sfxMatches.Count; i++)
            {
                var sfxMatch = sfxMatches[i];
                var sfxName = sfxMatch.Groups[1]?.ToString()?.ToLower();

                if (!File.Exists("sfx/" + sfxName + ".mp3"))
                {
                    parts[i * 2 + 2] = parts[i * 2] + " (" + parts[i * 2 + 1] + ")" + parts[i * 2 + 2];
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(parts[i * 2]))
                {
                    list.Add(new TTSMessage()
                    {
                        Voice = voice,
                        Message = parts[i * 2]
                    });
                }

                list.Add(new TTSMessage()
                {
                    Voice = voice,
                    File = $"sfx/{sfxName}.mp3"
                });
            }

            var lastContent = parts.Last();
            if (!string.IsNullOrWhiteSpace(lastContent))
            {
                list.Add(new TTSMessage()
                {
                    Voice = voice,
                    Message = lastContent
                });
            }
            return list;
        }

        private async Task<ChatCommandResult> CheckForChatCommand(string arguments, ChannelChatMessage message, IEnumerable<string> groups)
        {
            try
            {
                var commandResult = await _commands.Execute(arguments, message, groups);
                return commandResult;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed executing a chat command [message: {arguments}][chatter: {message.ChatterUserLogin}][chatter id: {message.ChatterUserId}][message id: {message.MessageId}]");
            }
            return ChatCommandResult.Fail;
        }

        private string FilterMessage(ChannelChatMessage message)
        {
            var msg = string.Join(string.Empty, message.Message.Fragments.Where(f => f.Type != "cheermote").Select(f => f.Text)).Trim();
            if (message.Reply != null)
                msg = msg.Substring(message.Reply.ParentUserLogin.Length + 2);

            // Replace filtered words.
            if (_user.RegexFilters != null)
            {
                foreach (var wf in _user.RegexFilters)
                {
                    if (wf.Search == null || wf.Replace == null)
                        continue;

                    if (wf.Regex != null)
                    {
                        try
                        {
                            msg = wf.Regex.Replace(msg, wf.Replace);
                            continue;
                        }
                        catch (Exception)
                        {
                            wf.Regex = null;
                        }
                    }

                    msg = msg.Replace(wf.Search, wf.Replace);
                }
            }
            return msg;
        }

        private ChatMessageEmoteUsage GetEmoteUsage(ChannelChatMessage message)
        {
            var emotesUsed = new HashSet<string>();
            var newEmotes = new Dictionary<string, string>();
            foreach (var fragment in message.Message.Fragments)
            {
                if (fragment.Emote != null)
                {
                    if (_emotes.Get(fragment.Text) == null)
                    {
                        newEmotes.Add(fragment.Text, fragment.Emote.Id);
                        _emotes.Add(fragment.Text, fragment.Emote.Id);
                    }
                    emotesUsed.Add(fragment.Emote.Id);
                    continue;
                }

                if (fragment.Mention != null)
                    continue;

                var text = fragment.Text.Trim();
                var textFragments = text.Split(' ');
                foreach (var f in textFragments)
                {
                    var emoteId = _emotes.Get(f);
                    if (emoteId != null)
                    {
                        emotesUsed.Add(emoteId);
                    }
                }
            }
            return new ChatMessageEmoteUsage(emotesUsed, newEmotes);
        }

        private int GetTotalBits(ChannelChatMessage message)
        {
            return message.Message.Fragments.Where(f => f.Type == "cheermote" && f.Cheermote != null)
                .Select(f => f.Cheermote!.Bits)
                .Sum();
        }

        private string GetGroupNameByBadgeName(string badgeName)
        {
            if (badgeName == "subscriber")
                return "subscribers";
            if (badgeName == "moderator")
                return "moderators";
            return badgeName.ToLower();
        }

        private IEnumerable<string> GetGroups(ChannelChatMessage message, long chatterId)
        {
            var defaultGroups = new string[] { "everyone" };
            var badgesGroups = message.Badges.Select(b => b.SetId).Select(GetGroupNameByBadgeName);
            var customGroups = _chatterGroupManager.GetGroupNamesFor(chatterId);
            return defaultGroups.Union(badgesGroups).Union(customGroups);
        }

        private IEnumerable<TTSMessage> GetPartialTTSMessages(string message, string defaultVoice)
        {
            var matches = _user.VoiceNameRegex?.Matches(message).ToArray();
            if (matches == null || matches.FirstOrDefault() == null || matches.First().Index < 0)
            {
                return [new TTSMessage()
                {
                    Voice = defaultVoice,
                    Message = message
                }];
            }

            return matches.Cast<Match>().SelectMany(match =>
            {
                var m = match.Groups["message"].Value;
                if (string.IsNullOrWhiteSpace(m))
                    return [];

                var voiceSelected = match.Groups["voice"].Value;
                voiceSelected = voiceSelected[0].ToString().ToUpper() + voiceSelected.Substring(1).ToLower();
                return HandlePartialMessage(voiceSelected, m);
            });
        }

        private string GetSelectedVoiceFor(long chatterId)
        {
            string? voiceSelected = _user.DefaultTTSVoice;
            if (_user.VoicesSelected?.ContainsKey(chatterId) == true)
            {
                var voiceId = _user.VoicesSelected[chatterId];
                if (_user.VoicesAvailable.TryGetValue(voiceId, out string? voiceName) && voiceName != null)
                {
                    if (_user.VoicesEnabled.Contains(voiceName) || chatterId == _user.OwnerId)
                        voiceSelected = voiceName;
                }
            }
            return voiceSelected ?? "Brian";
        }

        private bool HasPermission(ChannelChatMessage message, long chatterId, IEnumerable<string> groups, int bits)
        {
            var permissionPath = "tts.chat.messages.read";
            if (!string.IsNullOrWhiteSpace(message.ChannelPointsCustomRewardId))
                permissionPath = "tts.chat.redemptions.read";
            else if (bits > 0)
                permissionPath = "tts.chat.bits.read";

            return chatterId == _user.OwnerId ? true : _permissionManager.CheckIfAllowed(groups, permissionPath) == true;
        }

        private class ChatMessageEmoteUsage
        {
            public readonly HashSet<string> EmotesUsed = new HashSet<string>();
            public readonly IDictionary<string, string> NewEmotes = new Dictionary<string, string>();

            public ChatMessageEmoteUsage(HashSet<string> emotesUsed, IDictionary<string, string> newEmotes)
            {
                EmotesUsed = emotesUsed;
                NewEmotes = newEmotes;
            }
        }
    }
}