using System.Text.RegularExpressions;
using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TwitchChatTTS.Chat.Commands;
using TwitchChatTTS.Chat.Emotes;
using TwitchChatTTS.Chat.Groups;
using TwitchChatTTS.Chat.Groups.Permissions;
using TwitchChatTTS.Hermes.Socket;
using TwitchChatTTS.OBS.Socket;
using TwitchChatTTS.Twitch.Socket.Messages;

namespace TwitchChatTTS.Twitch.Socket.Handlers
{
    public class ChannelChatMessageHandler : ITwitchSocketHandler
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


        public ChannelChatMessageHandler(
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

        public async Task Execute(TwitchWebsocketClient sender, object data)
        {
            if (sender == null)
                return;
            if (data is not ChannelChatMessage message)
                return;

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

            var msg = message.Message.Text;
            var chatterId = long.Parse(message.ChatterUserId);
            var tasks = new List<Task>();

            var defaultGroups = new string[] { "everyone" };
            var badgesGroups = message.Badges.Select(b => b.SetId).Select(GetGroupNameByBadgeName);
            var customGroups = _chatterGroupManager.GetGroupNamesFor(chatterId);
            var groups = defaultGroups.Union(badgesGroups).Union(customGroups);

            try
            {
                var commandResult = await _commands.Execute(msg, message, groups);
                if (commandResult != ChatCommandResult.Unknown)
                    return; // new MessageResult(MessageStatus.Command, -1, -1);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed executing a chat command [message: {msg}][chatter: {message.ChatterUserLogin}][chatter id: {message.ChatterUserId}][message id: {message.MessageId}]");
                return;
            }

            if (message.Reply != null)
                msg = msg.Substring(message.Reply.ParentUserLogin.Length + 2);

            var permissionPath = "tts.chat.messages.read";
            if (!string.IsNullOrWhiteSpace(message.ChannelPointsCustomRewardId))
                permissionPath = "tts.chat.redemptions.read";

            var permission = chatterId == _user.OwnerId ? true : _permissionManager.CheckIfAllowed(groups, permissionPath);
            if (permission != true)
            {
                _logger.Debug($"Blocked message by {message.ChatterUserLogin}: {msg}");
                return; // new MessageResult(MessageStatus.Blocked, -1, -1);
            }

            // Keep track of emotes usage
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
            if (_obs.Streaming)
            {
                if (newEmotes.Any())
                    tasks.Add(_hermes.SendEmoteDetails(newEmotes));
                if (emotesUsed.Any())
                    tasks.Add(_hermes.SendEmoteUsage(message.MessageId, chatterId, emotesUsed));
                if (!_user.Chatters.Contains(chatterId))
                {
                    tasks.Add(_hermes.SendChatterDetails(chatterId, message.ChatterUserLogin));
                    _user.Chatters.Add(chatterId);
                }
            }

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

            // Determine the priority of this message
            int priority = _chatterGroupManager.GetPriorityFor(groups); // + m.SubscribedMonthCount * (m.IsSubscriber ? 10 : 5);

            // Determine voice selected.
            string voiceSelected = _user.DefaultTTSVoice;
            if (_user.VoicesSelected?.ContainsKey(chatterId) == true)
            {
                var voiceId = _user.VoicesSelected[chatterId];
                if (_user.VoicesAvailable.TryGetValue(voiceId, out string? voiceName) && voiceName != null)
                {
                    if (_user.VoicesEnabled.Contains(voiceName) || chatterId == _user.OwnerId)
                        voiceSelected = voiceName;
                }
            }

            // Determine additional voices used
            var matches = _user.VoiceNameRegex?.Matches(msg).ToArray();
            if (matches == null || matches.FirstOrDefault() == null || matches.First().Index < 0)
            {
                HandlePartialMessage(priority, voiceSelected, msg.Trim(), message);
                return; // new MessageResult(MessageStatus.None, _user.TwitchUserId, chatterId, emotesUsed);
            }

            HandlePartialMessage(priority, voiceSelected, msg.Substring(0, matches.First().Index).Trim(), message);
            foreach (Match match in matches)
            {
                var m = match.Groups[2].ToString();
                if (string.IsNullOrWhiteSpace(m))
                    continue;

                var voice = match.Groups[1].ToString();
                voice = voice[0].ToString().ToUpper() + voice.Substring(1).ToLower();
                HandlePartialMessage(priority, voice, m.Trim(), message);
            }

            if (tasks.Any())
                await Task.WhenAll(tasks);
        }

        private void HandlePartialMessage(int priority, string voice, string message, ChannelChatMessage e)
        {
            if (string.IsNullOrWhiteSpace(message))
                return;

            var parts = _sfxRegex.Split(message);
            var chatterId = long.Parse(e.ChatterUserId);
            var broadcasterId = long.Parse(e.BroadcasterUserId);
            var badgesString = string.Join(", ", e.Badges.Select(b => b.SetId + '|' + b.Id + '=' + b.Info));

            if (parts.Length == 1)
            {
                _logger.Information($"Username: {e.ChatterUserLogin}; User ID: {e.ChatterUserId}; Voice: {voice}; Priority: {priority}; Message: {message}; Reward Id: {e.ChannelPointsCustomRewardId}; {badgesString}");
                _player.Add(new TTSMessage()
                {
                    Voice = voice,
                    Message = message,
                    Timestamp = DateTime.UtcNow,
                    RoomId = broadcasterId,
                    ChatterId = chatterId,
                    MessageId = e.MessageId,
                    Badges = e.Badges,
                    Priority = priority
                });
                return;
            }

            var sfxMatches = _sfxRegex.Matches(message);
            var sfxStart = sfxMatches.FirstOrDefault()?.Index ?? message.Length;

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
                    _logger.Information($"Username: {e.ChatterUserLogin}; User ID: {e.ChatterUserId}; Voice: {voice}; Priority: {priority}; Message: {parts[i * 2]}; {badgesString}");
                    _player.Add(new TTSMessage()
                    {
                        Voice = voice,
                        Message = parts[i * 2],
                        Timestamp = DateTime.UtcNow,
                        RoomId = broadcasterId,
                        ChatterId = chatterId,
                        MessageId = e.MessageId,
                        Badges = e.Badges,
                        Priority = priority
                    });
                }

                _logger.Information($"Username: {e.ChatterUserLogin}; User ID: {e.ChatterUserId}; Voice: {voice}; Priority: {priority}; SFX: {sfxName}; {badgesString}");
                _player.Add(new TTSMessage()
                {
                    Voice = voice,
                    File = $"sfx/{sfxName}.mp3",
                    Timestamp = DateTime.UtcNow,
                    RoomId = broadcasterId,
                    ChatterId = chatterId,
                    MessageId = e.MessageId,
                    Badges = e.Badges,
                    Priority = priority
                });
            }

            if (!string.IsNullOrWhiteSpace(parts.Last()))
            {
                _logger.Information($"Username: {e.ChatterUserLogin}; User ID: {e.ChatterUserId}; Voice: {voice}; Priority: {priority}; Message: {parts.Last()}; {badgesString}");
                _player.Add(new TTSMessage()
                {
                    Voice = voice,
                    Message = parts.Last(),
                    Timestamp = DateTime.UtcNow,
                    RoomId = broadcasterId,
                    ChatterId = chatterId,
                    MessageId = e.MessageId,
                    Badges = e.Badges,
                    Priority = priority
                });
            }
        }

        private string GetGroupNameByBadgeName(string badgeName)
        {
            if (badgeName == "subscriber")
                return "subscribers";
            if (badgeName == "moderator")
                return "moderators";
            return badgeName.ToLower();
        }
    }
}