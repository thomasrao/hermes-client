using System.Text.RegularExpressions;
using TwitchLib.Client.Events;
using TwitchChatTTS.OBS.Socket;
using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using Microsoft.Extensions.Logging;
using TwitchChatTTS.Twitch;
using Microsoft.Extensions.DependencyInjection;
using TwitchChatTTS;
using TwitchChatTTS.Seven;


public class ChatMessageHandler {
    private ILogger<ChatMessageHandler> Logger { get; }
    private Configuration Configuration { get; }
    public EmoteCounter EmoteCounter { get; }
    private EmoteDatabase Emotes { get; }
    private TTSPlayer Player { get; }
    private OBSSocketClient? Client { get; }
    private TTSContext Context { get; }

    private Regex? voicesRegex;
    private Regex sfxRegex;


    public ChatMessageHandler(
        ILogger<ChatMessageHandler> logger,
        Configuration configuration,
        EmoteCounter emoteCounter,
        EmoteDatabase emotes,
        TTSPlayer player,
        [FromKeyedServices("obs")] SocketClient<WebSocketMessage> client,
        TTSContext context
    ) {
        Logger = logger;
        Configuration = configuration;
        EmoteCounter = emoteCounter;
        Emotes = emotes;
        Player = player;
        Client = client as OBSSocketClient;
        Context = context;

        voicesRegex = GenerateEnabledVoicesRegex();
        sfxRegex = new Regex(@"\(([A-Za-z0-9_-]+)\)");
    }


    public MessageResult Handle(OnMessageReceivedArgs e) {
        if (Configuration.Twitch?.TtsWhenOffline != true && Client?.Live != true)
            return MessageResult.Blocked;

        var m = e.ChatMessage;
        var msg = e.ChatMessage.Message;
        
        // Skip TTS messages
        if (m.IsVip || m.IsModerator || m.IsBroadcaster) {
            if (msg.ToLower().StartsWith("!skip ") || msg.ToLower() == "!skip")
                return MessageResult.Skip;
            
            if (msg.ToLower().StartsWith("!skipall ") || msg.ToLower() == "!skipall")
                return MessageResult.SkipAll;
        }

        if (Context.UsernameFilters.TryGetValue(m.Username, out TTSUsernameFilter? filter) && filter.Tag == "blacklisted") {
            Logger.LogTrace($"Blocked message by {m.Username}: {msg}");
            return MessageResult.Blocked;
        }

        // Replace filtered words.
        if (Context.WordFilters is not null) {
            foreach (var wf in Context.WordFilters) {
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

            var emoteId = Emotes?.Get(w);
            if (emoteId != null)
                emotesUsed.Add("7tv-" + emoteId);

            if (wordCounter[w] <= 4 && (emoteId == null || emotesUsed.Count <= 4))
                filteredMsg += w + " ";
        }
        msg = filteredMsg;

        // Adding twitch emotes to the counter.
        foreach (var emote in e.ChatMessage.EmoteSet.Emotes)
            emotesUsed.Add("twitch-" + emote.Id);
        
        if (long.TryParse(e.ChatMessage.UserId, out long userId))
            EmoteCounter.Add(userId, emotesUsed);
        if (emotesUsed.Any())
            Logger.LogDebug("Emote counters for user #" + userId + ": " + string.Join(" | ", emotesUsed.Select(e => e + "=" + EmoteCounter.Get(userId, e))));

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
        priority = (int) Math.Round(Math.Min(priority, -m.SubscribedMonthCount * (m.Badges.Any(b => b.Key == "subscriber") ? 1.2 : 1)));

        var matches = voicesRegex?.Matches(msg).ToArray() ?? new Match[0];
        int defaultEnd = matches.FirstOrDefault()?.Index ?? msg.Length;
        if (defaultEnd > 0) {
            HandlePartialMessage(priority, Context.DefaultVoice, msg.Substring(0, defaultEnd).Trim(), e);
        }

        foreach (Match match in matches) {
            var message = match.Groups[2].ToString();
            if (string.IsNullOrWhiteSpace(message)) {
                continue;
            }

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
            Logger.LogInformation($"Voice: {voice}; Priority: {priority}; Message: {message}; Month: {m.SubscribedMonthCount}; {badgesString}");
            Player.Add(new TTSMessage() {
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
                Logger.LogInformation($"Voice: {voice}; Priority: {priority}; Message: {parts[i * 2]}; Month: {m.SubscribedMonthCount}; {badgesString}");
                Player.Add(new TTSMessage() {
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

            Logger.LogInformation($"Voice: {voice}; Priority: {priority}; SFX: {sfxName}; Month: {m.SubscribedMonthCount}; {badgesString}");
            Player.Add(new TTSMessage() {
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
            Logger.LogInformation($"Voice: {voice}; Priority: {priority}; Message: {parts.Last()}; Month: {m.SubscribedMonthCount}; {badgesString}");
            Player.Add(new TTSMessage() {
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

    private Regex? GenerateEnabledVoicesRegex() {
        if (Context.EnabledVoices == null || Context.EnabledVoices.Count() <= 0) {
            return null;
        }

        var enabledVoicesString = string.Join("|", Context.EnabledVoices.Select(v => v.Label));
        return new Regex($@"\b({enabledVoicesString})\:(.*?)(?=\Z|\b(?:{enabledVoicesString})\:)", RegexOptions.IgnoreCase);
    }
}