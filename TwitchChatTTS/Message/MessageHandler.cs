using System.Text.RegularExpressions;
using TwitchLib.Client.Events;
using TwitchChatTTS.Hermes;


public class ChatMessageHandler {
    private TTSPlayer Player { get; }
    public string DefaultVoice { get; set; }
    public IEnumerable<TTSVoice> EnabledVoices { get; }
    public Dictionary<string, TTSUsernameFilter> UsernameFilters { get; }
    public IEnumerable<TTSWordFilter> WordFilters { get; }

    private Regex voicesRegex;
    private Regex sfxRegex;


    public ChatMessageHandler(TTSPlayer player, string defaultVoice, IEnumerable<TTSVoice> enabledVoices, Dictionary<string, TTSUsernameFilter> usernameFilters, IEnumerable<TTSWordFilter> wordFilters) {
        Player = player;
        DefaultVoice = defaultVoice;
        EnabledVoices = enabledVoices;
        UsernameFilters = usernameFilters;
        WordFilters = wordFilters;

        voicesRegex = GenerateEnabledVoicesRegex();
        sfxRegex = new Regex(@"\(([A-Za-z0-9_-]+)\)");
    }


    public MessageResult Handle(OnMessageReceivedArgs e) {
        var m = e.ChatMessage;
        var msg = e.ChatMessage.Message;

        // Skip TTS messages
        if ((m.IsVip || m.IsModerator || m.IsBroadcaster) && (msg.ToLower().StartsWith("!skip ") || msg.ToLower() == "!skip")) {
            return MessageResult.Skip;
        }

        if (UsernameFilters.TryGetValue(m.Username, out TTSUsernameFilter filter) && filter.tag == "blacklisted") {
            return MessageResult.Blocked;
        }

        // Ensure we can send it via the web.
        var alphanumeric = new Regex(@"[^a-zA-Z0-9!@#$%&\^*+\-_(),+':;?.,\[\]\s\\/~`]");
        msg = alphanumeric.Replace(msg, "");

        // Filter highly repetitive words (like emotes) from the message.
        var words = msg.Split(" ");
        var wordCounter = new Dictionary<string, int>();
        string filteredMsg = string.Empty;
        foreach (var w in words) {
            if (wordCounter.ContainsKey(w)) {
                wordCounter[w]++;
            } else {
                wordCounter.Add(w, 1);
            }

            if (wordCounter[w] < 5) {
                filteredMsg += w + " ";
            }
        }
        msg = filteredMsg;

        foreach (var wf in WordFilters) {
            if (wf.IsRegex) {
                try {
                    var regex = new Regex(wf.search);
                    msg = regex.Replace(msg, wf.replace);
                    continue;
                } catch (Exception ex) {
                    wf.IsRegex = false;
                }
            }

            msg = msg.Replace(wf.search, wf.replace);
        }

        int priority = 0;
        if (m.IsStaff) {
            priority = int.MinValue;
        } else if (filter?.tag == "priority") {
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
        priority = (int) Math.Round(Math.Min(priority, -m.SubscribedMonthCount * (m.Badges.Any(b => b.Key == "subscriber" && b.Value == "1") ? 1.2 : 1)));

        var matches = voicesRegex.Matches(msg);
        int defaultEnd = matches.FirstOrDefault()?.Index ?? msg.Length;
        if (defaultEnd > 0) {
            HandlePartialMessage(priority, DefaultVoice, msg.Substring(0, defaultEnd).Trim(), e);
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
            Console.WriteLine($"Voice: {voice}; Priority: {priority}; Message: {message}; Month: {m.SubscribedMonthCount}; {badgesString}");
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
                Console.WriteLine($"Voice: {voice}; Priority: {priority}; Message: {parts[i * 2]}; Month: {m.SubscribedMonthCount}; {badgesString}");
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

            Console.WriteLine($"Voice: {voice}; Priority: {priority}; SFX: {sfxName}; Month: {m.SubscribedMonthCount}; {badgesString}");
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
            Console.WriteLine($"Voice: {voice}; Priority: {priority}; Message: {parts.Last()}; Month: {m.SubscribedMonthCount}; {badgesString}");
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

    private Regex GenerateEnabledVoicesRegex() {
        if (EnabledVoices == null || EnabledVoices.Count() <= 0) {
            return null;
        }

        var enabledVoicesString = string.Join("|", EnabledVoices.Select(v => v.label));
        return new Regex($@"\b({enabledVoicesString})\:(.*?)(?=\Z|\b(?:{enabledVoicesString})\:)", RegexOptions.IgnoreCase);
    }
}