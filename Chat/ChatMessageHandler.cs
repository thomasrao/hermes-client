using System.Text.RegularExpressions;
using TwitchLib.Client.Events;
using TwitchChatTTS.OBS.Socket;
using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using TwitchChatTTS;
using TwitchChatTTS.Seven;
using TwitchChatTTS.Chat.Commands;


public class ChatMessageHandler {
    private ILogger<ChatMessageHandler> _logger { get; }
    private Configuration _configuration { get; }
    public EmoteCounter _emoteCounter { get; }
    private EmoteDatabase _emotes { get; }
    private TTSPlayer _player { get; }
    private ChatCommandManager _commands { get; }
    private OBSSocketClient? _obsClient { get; }
    private IServiceProvider _serviceProvider { get; }

    private Regex sfxRegex;


    public ChatMessageHandler(
        ILogger<ChatMessageHandler> logger,
        Configuration configuration,
        EmoteCounter emoteCounter,
        EmoteDatabase emotes,
        TTSPlayer player,
        ChatCommandManager commands,
        [FromKeyedServices("obs")] SocketClient<WebSocketMessage> client,
        IServiceProvider serviceProvider
    ) {
        _logger = logger;
        _configuration = configuration;
        _emoteCounter = emoteCounter;
        _emotes = emotes;
        _player = player;
        _commands = commands;
        _obsClient = client as OBSSocketClient;
        _serviceProvider = serviceProvider;

        sfxRegex = new Regex(@"\(([A-Za-z0-9_-]+)\)");
    }


    public async Task<MessageResult> Handle(OnMessageReceivedArgs e) {
        if (_configuration.Twitch?.TtsWhenOffline != true && _obsClient?.Live == false)
            return MessageResult.Blocked;
        
        var user = _serviceProvider.GetRequiredService<User>();
        var m = e.ChatMessage;
        var msg = e.ChatMessage.Message;
        var chatterId = long.Parse(m.UserId);

        var blocked = user.ChatterFilters.TryGetValue(m.Username, out TTSUsernameFilter? filter) && filter.Tag == "blacklisted";

        if (!blocked || m.IsBroadcaster) {
            try {
                var commandResult = await _commands.Execute(msg, m);
                if (commandResult != ChatCommandResult.Unknown) {
                    return MessageResult.Command;
                }
            } catch (Exception ex) {
                _logger.LogError(ex, "Failed at executing command.");
            }
        }

        if (blocked) {
            _logger.LogTrace($"Blocked message by {m.Username}: {msg}");
            return MessageResult.Blocked;
        }

        // Replace filtered words.
        if (user.RegexFilters != null) {
            foreach (var wf in user.RegexFilters) {
                if (wf.Search == null || wf.Replace == null)
                    continue;
                
                if (wf.IsRegex) {
                    try {
                        var regex = new Regex(wf.Search);
                        msg = regex.Replace(msg, wf.Replace);
                        continue;
                    } catch (Exception) {
                        wf.IsRegex = false;
                    }
                }

                msg = msg.Replace(wf.Search, wf.Replace);
            }
        }

        // Filter highly repetitive words (like emotes) from the message.
        int totalEmoteUsed = 0;
        var emotesUsed = new HashSet<string>();
        var words = msg.Split(" ");
        var wordCounter = new Dictionary<string, int>();
        string filteredMsg = string.Empty;
        foreach (var w in words) {
            if (wordCounter.ContainsKey(w)) {
                wordCounter[w]++;
            } else {
                wordCounter.Add(w, 1);
            }

            var emoteId = _emotes?.Get(w);
            if (emoteId == null)
                emoteId = m.EmoteSet.Emotes.FirstOrDefault(e => e.Name == w)?.Id;
            if (emoteId != null) {
                emotesUsed.Add(emoteId);
                totalEmoteUsed++;
            }

            if (wordCounter[w] <= 4 && (emoteId == null || totalEmoteUsed <= 5))
                filteredMsg += w + " ";
        }
        msg = filteredMsg;

        // Adding twitch emotes to the counter.
        foreach (var emote in e.ChatMessage.EmoteSet.Emotes) {
            _logger.LogTrace("Twitch emote name used: " + emote.Name);
            emotesUsed.Add(emote.Id);
        }
        
        if (long.TryParse(e.ChatMessage.UserId, out long userId))
            _emoteCounter.Add(userId, emotesUsed);
        if (emotesUsed.Any())
            _logger.LogDebug("Emote counters for user #" + userId + ": " + string.Join(" | ", emotesUsed.Select(e => e + "=" + _emoteCounter.Get(userId, e))));

        // Determine the priority of this message
        int priority = 0;
        if (m.IsStaff) {
            priority = int.MinValue;
        } else if (filter?.Tag == "priority") {
            priority = int.MinValue + 1;
        } else if (m.IsModerator) {
            priority = -100;
        } else if (m.IsVip) {
            priority = -10;
        } else if (m.IsPartner) {
            priority = -5;
        } else if (m.IsHighlighted) {
            priority = -1;
        }
        priority = Math.Min(priority, -m.SubscribedMonthCount * (m.IsSubscriber ? 2 : 1));

        // Determine voice selected.
        string voiceSelected = user.DefaultTTSVoice;
        if (user.VoicesSelected?.ContainsKey(userId) == true) {
            var voiceId = user.VoicesSelected[userId];
            if (user.VoicesAvailable.TryGetValue(voiceId, out string? voiceName) && voiceName != null) {
                voiceSelected = voiceName;
            }
        }

        // Determine additional voices used
        var voicesRegex = user.GenerateEnabledVoicesRegex();
        var matches = voicesRegex?.Matches(msg).ToArray();
        if (matches == null || matches.FirstOrDefault() == null || matches.FirstOrDefault().Index == 0) {
            HandlePartialMessage(priority, voiceSelected, msg.Trim(), e);
            return MessageResult.None;
        }

        HandlePartialMessage(priority, voiceSelected, msg.Substring(0, matches.FirstOrDefault().Index).Trim(), e);
        foreach (Match match in matches) {
            var message = match.Groups[2].ToString();
            if (string.IsNullOrWhiteSpace(message))
                continue;

            var voice = match.Groups[1].ToString();
            voice = voice[0].ToString().ToUpper() + voice.Substring(1).ToLower();
            HandlePartialMessage(priority, voice, message.Trim(), e);
        }

        return MessageResult.None;
    }

    private void HandlePartialMessage(int priority, string voice, string message, OnMessageReceivedArgs e) {
        if (string.IsNullOrWhiteSpace(message)) {
            return;
        }

        var m = e.ChatMessage;
        var parts = sfxRegex.Split(message);
        var badgesString = string.Join(", ", e.ChatMessage.Badges.Select(b => b.Key + " = " + b.Value));
        
        if (parts.Length == 1) {
            _logger.LogInformation($"Voice: {voice}; Priority: {priority}; Message: {message}; Month: {m.SubscribedMonthCount}; {badgesString}");
            _player.Add(new TTSMessage() {
                Voice = voice,
                Message = message,
                Moderator = m.IsModerator,
                Timestamp = DateTime.UtcNow,
                Username = m.Username,
                Bits = m.Bits,
                Badges = m.Badges,
                Priority = priority
            });
            return;
        }

        var sfxMatches = sfxRegex.Matches(message);
        var sfxStart = sfxMatches.FirstOrDefault()?.Index ?? message.Length;

        for (var i = 0; i < sfxMatches.Count; i++) {
            var sfxMatch = sfxMatches[i];
            var sfxName = sfxMatch.Groups[1]?.ToString()?.ToLower();

            if (!File.Exists("sfx/" + sfxName + ".mp3")) {
                parts[i * 2 + 2] = parts[i * 2] + " (" + parts[i * 2 + 1] + ")" + parts[i * 2 + 2];
                continue;
            }

            if (!string.IsNullOrWhiteSpace(parts[i * 2])) {
                _logger.LogInformation($"Username: {m.Username}; User ID: {m.UserId}; Voice: {voice}; Priority: {priority}; Message: {parts[i * 2]}; Month: {m.SubscribedMonthCount}; {badgesString}");
                _player.Add(new TTSMessage() {
                    Voice = voice,
                    Message = parts[i * 2],
                    Moderator = m.IsModerator,
                    Timestamp = DateTime.UtcNow,
                    Username = m.Username,
                    Bits = m.Bits,
                    Badges = m.Badges,
                    Priority = priority
                });
            }

            _logger.LogInformation($"Username: {m.Username}; User ID: {m.UserId}; Voice: {voice}; Priority: {priority}; SFX: {sfxName}; Month: {m.SubscribedMonthCount}; {badgesString}");
            _player.Add(new TTSMessage() {
                Voice = voice,
                Message = sfxName,
                File = $"sfx/{sfxName}.mp3",
                Moderator = m.IsModerator,
                Timestamp = DateTime.UtcNow,
                Username = m.Username,
                Bits = m.Bits,
                Badges = m.Badges,
                Priority = priority
            });
        }

        if (!string.IsNullOrWhiteSpace(parts.Last())) {
            _logger.LogInformation($"Username: {m.Username}; User ID: {m.UserId}; Voice: {voice}; Priority: {priority}; Message: {parts.Last()}; Month: {m.SubscribedMonthCount}; {badgesString}");
            _player.Add(new TTSMessage() {
                Voice = voice,
                Message = parts.Last(),
                Moderator = m.IsModerator,
                Timestamp = DateTime.UtcNow,
                Username = m.Username,
                Bits = m.Bits,
                Badges = m.Badges,
                Priority = priority
            });
        }
    }
}