using System.Text.RegularExpressions;
using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TwitchChatTTS.Chat.Emotes;
using TwitchChatTTS.Chat.Speech;
using TwitchChatTTS.Hermes.Socket;
using TwitchChatTTS.OBS.Socket;
using TwitchChatTTS.Twitch.Socket;
using TwitchChatTTS.Twitch.Socket.Messages;

namespace TwitchChatTTS.Chat.Messaging
{
    public class ChatMessageReader : IChatMessageReader
    {
        private readonly User _user;
        private readonly TTSPlayer _player;
        private readonly IEmoteDatabase _emotes;
        private readonly OBSSocketClient _obs;
        private readonly HermesSocketClient _hermes;
        private readonly Configuration _configuration;
        private readonly ILogger _logger;

        private readonly Regex _sfxRegex;


        public ChatMessageReader(
            User user,
            TTSPlayer player,
            IEmoteDatabase emotes,
            [FromKeyedServices("hermes")] SocketClient<WebSocketMessage> hermes,
            [FromKeyedServices("obs")] SocketClient<WebSocketMessage> obs,
            Configuration configuration,
            ILogger logger
        )
        {
            _user = user;
            _player = player;
            _emotes = emotes;
            _obs = (obs as OBSSocketClient)!;
            _hermes = (hermes as HermesSocketClient)!;
            _configuration = configuration;
            _logger = logger;

            _sfxRegex = new Regex(@"\(([A-Za-z0-9_-]+)\)", RegexOptions.Compiled);
            _logger = logger;
        }

        public async Task Read(TwitchWebsocketClient sender, long broadcasterId, long? chatterId, string? chatterLogin, string? messageId, TwitchReplyInfo? reply, TwitchChatFragment[] fragments, int priority)
        {
            if (_hermes.Connected && !_hermes.Ready)
            {
                _logger.Debug($"TTS is not yet ready. Ignoring chat messages [message id: {messageId}]");
                return;
            }
            if (_configuration.Twitch?.TtsWhenOffline != true && !_obs.Streaming)
            {
                _logger.Debug($"OBS is not streaming. Ignoring chat messages [message id: {messageId}]");
                return;
            }

            var emoteUsage = GetEmoteUsage(fragments);
            var tasks = new List<Task>();
            if (_obs.Streaming)
            {
                if (emoteUsage.NewEmotes.Any())
                    tasks.Add(_hermes.SendEmoteDetails(emoteUsage.NewEmotes));
                if (emoteUsage.EmotesUsed.Any() && messageId != null && chatterId != null)
                    tasks.Add(_hermes.SendEmoteUsage(messageId, chatterId.Value, emoteUsage.EmotesUsed));
                if (!string.IsNullOrEmpty(chatterLogin) && chatterId != null && !_user.Chatters.Contains(chatterId.Value))
                {
                    tasks.Add(_hermes.SendChatterDetails(chatterId.Value, chatterLogin));
                    _user.Chatters.Add(chatterId.Value);
                }
            }

            if (chatterId != null && _user.Raids.TryGetValue(broadcasterId.ToString(), out var raid) && !raid.Chatters.Contains(chatterId.Value))
            {
                _logger.Information($"Potential chat message from raider ignored due to potential raid message spam [chatter: {chatterLogin}][chatter id: {chatterId}]");
                return;
            }

            var msg = FilterMessage(fragments, reply);
            string voiceSelected = chatterId == null ? _user.DefaultTTSVoice : GetSelectedVoiceFor(chatterId.Value);
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

        private string FilterMessage(TwitchChatFragment[] fragments, TwitchReplyInfo? reply)
        {
            var msg = string.Join(string.Empty, fragments.Where(f => f.Type != "cheermote").Select(f => f.Text)).Trim();
            if (reply != null)
                msg = msg.Substring(reply.ParentUserLogin.Length + 2);

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

        private ChatMessageEmoteUsage GetEmoteUsage(TwitchChatFragment[] fragments)
        {
            var emotesUsed = new HashSet<string>();
            var newEmotes = new Dictionary<string, string>();
            foreach (var fragment in fragments)
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