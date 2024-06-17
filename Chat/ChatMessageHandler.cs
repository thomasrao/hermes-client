using System.Text.RegularExpressions;
using TwitchLib.Client.Events;
using TwitchChatTTS.OBS.Socket;
using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using Serilog;
using Microsoft.Extensions.DependencyInjection;
using TwitchChatTTS;
using TwitchChatTTS.Seven;
using TwitchChatTTS.Chat.Commands;
using TwitchChatTTS.Hermes.Socket;
using HermesSocketLibrary.Socket.Data;


public class ChatMessageHandler
{
    private ILogger _logger { get; }
    private Configuration _configuration { get; }
    private EmoteDatabase _emotes { get; }
    private TTSPlayer _player { get; }
    private ChatCommandManager _commands { get; }
    private OBSSocketClient? _obsClient { get; }
    private HermesSocketClient? _hermesClient { get; }
    private IServiceProvider _serviceProvider { get; }

    private Regex sfxRegex;
    private HashSet<long> _chatters;

    public HashSet<long> Chatters { get => _chatters; set => _chatters = value; }


    public ChatMessageHandler(
        TTSPlayer player,
        ChatCommandManager commands,
        EmoteDatabase emotes,
        [FromKeyedServices("obs")] SocketClient<WebSocketMessage> obsClient,
        [FromKeyedServices("hermes")] SocketClient<WebSocketMessage> hermesClient,
        Configuration configuration,
        IServiceProvider serviceProvider,
        ILogger logger
    )
    {
        _player = player;
        _commands = commands;
        _emotes = emotes;
        _obsClient = obsClient as OBSSocketClient;
        _hermesClient = hermesClient as HermesSocketClient;
        _configuration = configuration;
        _serviceProvider = serviceProvider;
        _logger = logger;

        _chatters = null;
        sfxRegex = new Regex(@"\(([A-Za-z0-9_-]+)\)");
    }


    public async Task<MessageResult> Handle(OnMessageReceivedArgs e)
    {
        if (_obsClient == null || _hermesClient == null || _obsClient.Connected && _chatters == null)
            return new MessageResult(MessageStatus.NotReady, -1, -1);
        if (_configuration.Twitch?.TtsWhenOffline != true && _obsClient.Live == false)
            return new MessageResult(MessageStatus.NotReady, -1, -1);

        var user = _serviceProvider.GetRequiredService<User>();
        var m = e.ChatMessage;
        var msg = e.ChatMessage.Message;
        var chatterId = long.Parse(m.UserId);
        var tasks = new List<Task>();

        var blocked = user.ChatterFilters.TryGetValue(m.Username, out TTSUsernameFilter? filter) && filter.Tag == "blacklisted";
        if (!blocked || m.IsBroadcaster)
        {
            try
            {
                var commandResult = await _commands.Execute(msg, m);
                if (commandResult != ChatCommandResult.Unknown)
                    return new MessageResult(MessageStatus.Command, -1, -1);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed at executing command.");
            }
        }

        if (blocked)
        {
            _logger.Debug($"Blocked message by {m.Username}: {msg}");
            return new MessageResult(MessageStatus.Blocked, -1, -1);
        }

        if (_obsClient.Connected && !_chatters.Contains(chatterId))
        {
            tasks.Add(_hermesClient.Send(6, new ChatterMessage()
            {
                Id = chatterId,
                Name = m.Username
            }));
            _chatters.Add(chatterId);
        }

        // Filter highly repetitive words (like emotes) from the message.
        int totalEmoteUsed = 0;
        var emotesUsed = new HashSet<string>();
        var words = msg.Split(" ");
        var wordCounter = new Dictionary<string, int>();
        string filteredMsg = string.Empty;
        var newEmotes = new Dictionary<string, string>();
        foreach (var w in words)
        {
            if (wordCounter.ContainsKey(w))
            {
                wordCounter[w]++;
            }
            else
            {
                wordCounter.Add(w, 1);
            }

            var emoteId = _emotes.Get(w);
            if (emoteId == null)
            {
                emoteId = m.EmoteSet.Emotes.FirstOrDefault(e => e.Name == w)?.Id;
                if (emoteId != null)
                {
                    newEmotes.Add(emoteId, w);
                    _emotes.Add(w, emoteId);
                }
            }
            if (emoteId != null)
            {
                emotesUsed.Add(emoteId);
                totalEmoteUsed++;
            }

            if (wordCounter[w] <= 4 && (emoteId == null || totalEmoteUsed <= 5))
                filteredMsg += w + " ";
        }
        if (_obsClient.Connected && newEmotes.Any())
            tasks.Add(_hermesClient.Send(7, new EmoteDetailsMessage()
            {
                Emotes = newEmotes
            }));
        msg = filteredMsg;

        // Replace filtered words.
        if (user.RegexFilters != null)
        {
            foreach (var wf in user.RegexFilters)
            {
                if (wf.Search == null || wf.Replace == null)
                    continue;

                if (wf.IsRegex)
                {
                    try
                    {
                        var regex = new Regex(wf.Search);
                        msg = regex.Replace(msg, wf.Replace);
                        continue;
                    }
                    catch (Exception)
                    {
                        wf.IsRegex = false;
                    }
                }

                msg = msg.Replace(wf.Search, wf.Replace);
            }
        }

        // Determine the priority of this message
        int priority = 0;
        if (m.IsStaff)
        {
            priority = int.MinValue;
        }
        else if (filter?.Tag == "priority")
        {
            priority = int.MinValue + 1;
        }
        else if (m.IsModerator)
        {
            priority = -100;
        }
        else if (m.IsVip)
        {
            priority = -10;
        }
        else if (m.IsPartner)
        {
            priority = -5;
        }
        else if (m.IsHighlighted)
        {
            priority = -1;
        }
        priority = Math.Min(priority, -m.SubscribedMonthCount * (m.IsSubscriber ? 2 : 1));

        // Determine voice selected.
        string voiceSelected = user.DefaultTTSVoice;
        if (long.TryParse(e.ChatMessage.UserId, out long userId) && user.VoicesSelected?.ContainsKey(userId) == true)
        {
            var voiceId = user.VoicesSelected[userId];
            if (user.VoicesAvailable.TryGetValue(voiceId, out string? voiceName) && voiceName != null)
            {
                voiceSelected = voiceName;
            }
        }

        // Determine additional voices used
        var matches = user.WordFilterRegex?.Matches(msg).ToArray();
        if (matches == null || matches.FirstOrDefault() == null || matches.First().Index < 0)
        {
            HandlePartialMessage(priority, voiceSelected, msg.Trim(), e);
            return new MessageResult(MessageStatus.None, user.TwitchUserId, chatterId, emotesUsed);
        }

        HandlePartialMessage(priority, voiceSelected, msg.Substring(0, matches.First().Index).Trim(), e);
        foreach (Match match in matches)
        {
            var message = match.Groups[2].ToString();
            if (string.IsNullOrWhiteSpace(message))
                continue;

            var voice = match.Groups[1].ToString();
            voice = voice[0].ToString().ToUpper() + voice.Substring(1).ToLower();
            HandlePartialMessage(priority, voice, message.Trim(), e);
        }

        if (tasks.Any())
            await Task.WhenAll(tasks);

        return new MessageResult(MessageStatus.None, user.TwitchUserId, chatterId, emotesUsed);
    }

    private void HandlePartialMessage(int priority, string voice, string message, OnMessageReceivedArgs e)
    {
        if (string.IsNullOrWhiteSpace(message))
        {
            return;
        }

        var m = e.ChatMessage;
        var parts = sfxRegex.Split(message);
        var badgesString = string.Join(", ", e.ChatMessage.Badges.Select(b => b.Key + " = " + b.Value));

        if (parts.Length == 1)
        {
            _logger.Information($"Username: {m.Username}; User ID: {m.UserId}; Voice: {voice}; Priority: {priority}; Message: {message}; Month: {m.SubscribedMonthCount}; {badgesString}");
            _player.Add(new TTSMessage()
            {
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
                _logger.Information($"Username: {m.Username}; User ID: {m.UserId}; Voice: {voice}; Priority: {priority}; Message: {parts[i * 2]}; Month: {m.SubscribedMonthCount}; {badgesString}");
                _player.Add(new TTSMessage()
                {
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

            _logger.Information($"Username: {m.Username}; User ID: {m.UserId}; Voice: {voice}; Priority: {priority}; SFX: {sfxName}; Month: {m.SubscribedMonthCount}; {badgesString}");
            _player.Add(new TTSMessage()
            {
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

        if (!string.IsNullOrWhiteSpace(parts.Last()))
        {
            _logger.Information($"Username: {m.Username}; User ID: {m.UserId}; Voice: {voice}; Priority: {priority}; Message: {parts.Last()}; Month: {m.SubscribedMonthCount}; {badgesString}");
            _player.Add(new TTSMessage()
            {
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