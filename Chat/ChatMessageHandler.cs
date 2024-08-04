// using System.Text.RegularExpressions;
// using TwitchLib.Client.Events;
// using Serilog;
// using TwitchChatTTS;
// using TwitchChatTTS.Chat.Commands;
// using TwitchChatTTS.Hermes.Socket;
// using TwitchChatTTS.Chat.Groups.Permissions;
// using TwitchChatTTS.Chat.Groups;
// using TwitchChatTTS.Chat.Emotes;
// using Microsoft.Extensions.DependencyInjection;
// using CommonSocketLibrary.Common;
// using CommonSocketLibrary.Abstract;
// using TwitchChatTTS.OBS.Socket;


// public class ChatMessageHandler
// {
//     private readonly User _user;
//     private readonly TTSPlayer _player;
//     private readonly CommandManager _commands;
//     private readonly IGroupPermissionManager _permissionManager;
//     private readonly IChatterGroupManager _chatterGroupManager;
//     private readonly IEmoteDatabase _emotes;
//     private readonly OBSSocketClient _obs;
//     private readonly HermesSocketClient _hermes;
//     private readonly Configuration _configuration;

//     private readonly ILogger _logger;

//     private Regex _sfxRegex;
//     private HashSet<long> _chatters;

//     public HashSet<long> Chatters { get => _chatters; set => _chatters = value; }


//     public ChatMessageHandler(
//         User user,
//         TTSPlayer player,
//         CommandManager commands,
//         IGroupPermissionManager permissionManager,
//         IChatterGroupManager chatterGroupManager,
//         IEmoteDatabase emotes,
//         [FromKeyedServices("hermes")] SocketClient<WebSocketMessage> hermes,
//         [FromKeyedServices("obs")] SocketClient<WebSocketMessage> obs,
//         Configuration configuration,
//         ILogger logger
//     )
//     {
//         _user = user;
//         _player = player;
//         _commands = commands;
//         _permissionManager = permissionManager;
//         _chatterGroupManager = chatterGroupManager;
//         _emotes = emotes;
//         _obs = (obs as OBSSocketClient)!;
//         _hermes = (hermes as HermesSocketClient)!;
//         _configuration = configuration;
//         _logger = logger;

//         _chatters = new HashSet<long>();
//         _sfxRegex = new Regex(@"\(([A-Za-z0-9_-]+)\)");
//     }


//     public async Task<MessageResult> Handle(OnMessageReceivedArgs e)
//     {
//         var m = e.ChatMessage;

//         if (_hermes.Connected && !_hermes.Ready)
//         {
//             _logger.Debug($"TTS is not yet ready. Ignoring chat messages [message id: {m.Id}]");
//             return new MessageResult(MessageStatus.NotReady, -1, -1);
//         }
//         if (_configuration.Twitch?.TtsWhenOffline != true && !_obs.Streaming)
//         {
//             _logger.Debug($"OBS is not streaming. Ignoring chat messages [message id: {m.Id}]");
//             return new MessageResult(MessageStatus.NotReady, -1, -1);
//         }


//         var msg = e.ChatMessage.Message;
//         var chatterId = long.Parse(m.UserId);
//         var tasks = new List<Task>();

//         var checks = new bool[] { true, m.IsSubscriber, m.IsVip, m.IsModerator, m.IsBroadcaster };
//         var defaultGroups = new string[] { "everyone", "subscribers", "vip", "moderators", "broadcaster" };
//         var customGroups = _chatterGroupManager.GetGroupNamesFor(chatterId);
//         var groups = defaultGroups.Where((e, i) => checks[i]).Union(customGroups);

//         try
//         {
//             var commandResult = await _commands.Execute(msg, m, groups);
//             if (commandResult != ChatCommandResult.Unknown)
//                 return new MessageResult(MessageStatus.Command, -1, -1);
//         }
//         catch (Exception ex)
//         {
//             _logger.Error(ex, $"Failed executing a chat command [message: {msg}][chatter: {m.Username}][chatter id: {m.UserId}][message id: {m.Id}]");
//         }

//         var permissionPath = "tts.chat.messages.read";
//         if (!string.IsNullOrWhiteSpace(m.CustomRewardId))
//             permissionPath = "tts.chat.redemptions.read";

//         var permission = chatterId == _user.OwnerId ? true : _permissionManager.CheckIfAllowed(groups, permissionPath);
//         if (permission != true)
//         {
//             _logger.Debug($"Blocked message by {m.Username}: {msg}");
//             return new MessageResult(MessageStatus.Blocked, -1, -1);
//         }

//         if (_obs.Streaming && !_chatters.Contains(chatterId))
//         {
//             tasks.Add(_hermes.SendChatterDetails(chatterId, m.Username));
//             _chatters.Add(chatterId);
//         }

//         // Filter highly repetitive words (like emotes) from the message.
//         int totalEmoteUsed = 0;
//         var emotesUsed = new HashSet<string>();
//         var words = msg.Split(' ');
//         var wordCounter = new Dictionary<string, int>();
//         string filteredMsg = string.Empty;
//         var newEmotes = new Dictionary<string, string>();
//         foreach (var w in words)
//         {
//             if (wordCounter.ContainsKey(w))
//                 wordCounter[w]++;
//             else
//                 wordCounter.Add(w, 1);

//             var emoteId = _emotes.Get(w);
//             if (emoteId == null)
//             {
//                 emoteId = m.EmoteSet.Emotes.FirstOrDefault(e => e.Name == w)?.Id;
//                 if (emoteId != null)
//                 {
//                     newEmotes.Add(emoteId, w);
//                     _emotes.Add(w, emoteId);
//                 }
//             }
//             if (emoteId != null)
//             {
//                 emotesUsed.Add(emoteId);
//                 totalEmoteUsed++;
//             }

//             if (wordCounter[w] <= 4 && (emoteId == null || totalEmoteUsed <= 5))
//                 filteredMsg += w + " ";
//         }
//         if (_obs.Streaming && newEmotes.Any())
//             tasks.Add(_hermes.SendEmoteDetails(newEmotes));
//         msg = filteredMsg;

//         // Replace filtered words.
//         if (_user.RegexFilters != null)
//         {
//             foreach (var wf in _user.RegexFilters)
//             {
//                 if (wf.Search == null || wf.Replace == null)
//                     continue;

//                 if (wf.IsRegex)
//                 {
//                     try
//                     {
//                         var regex = new Regex(wf.Search);
//                         msg = regex.Replace(msg, wf.Replace);
//                         continue;
//                     }
//                     catch (Exception)
//                     {
//                         wf.IsRegex = false;
//                     }
//                 }

//                 msg = msg.Replace(wf.Search, wf.Replace);
//             }
//         }

//         // Determine the priority of this message
//         int priority = _chatterGroupManager.GetPriorityFor(groups) + m.SubscribedMonthCount * (m.IsSubscriber ? 10 : 5);

//         // Determine voice selected.
//         string voiceSelected = _user.DefaultTTSVoice;
//         if (_user.VoicesSelected?.ContainsKey(chatterId) == true)
//         {
//             var voiceId = _user.VoicesSelected[chatterId];
//             if (_user.VoicesAvailable.TryGetValue(voiceId, out string? voiceName) && voiceName != null)
//             {
//                 if (_user.VoicesEnabled.Contains(voiceName) || chatterId == _user.OwnerId || m.IsStaff)
//                 {
//                     voiceSelected = voiceName;
//                 }
//             }
//         }

//         // Determine additional voices used
//         var matches = _user.WordFilterRegex?.Matches(msg).ToArray();
//         if (matches == null || matches.FirstOrDefault() == null || matches.First().Index < 0)
//         {
//             HandlePartialMessage(priority, voiceSelected, msg.Trim(), e);
//             return new MessageResult(MessageStatus.None, _user.TwitchUserId, chatterId, emotesUsed);
//         }

//         HandlePartialMessage(priority, voiceSelected, msg.Substring(0, matches.First().Index).Trim(), e);
//         foreach (Match match in matches)
//         {
//             var message = match.Groups[2].ToString();
//             if (string.IsNullOrWhiteSpace(message))
//                 continue;

//             var voice = match.Groups[1].ToString();
//             voice = voice[0].ToString().ToUpper() + voice.Substring(1).ToLower();
//             HandlePartialMessage(priority, voice, message.Trim(), e);
//         }

//         if (tasks.Any())
//             await Task.WhenAll(tasks);

//         return new MessageResult(MessageStatus.None, _user.TwitchUserId, chatterId, emotesUsed);
//     }

//     private void HandlePartialMessage(int priority, string voice, string message, OnMessageReceivedArgs e)
//     {
//         if (string.IsNullOrWhiteSpace(message))
//             return;

//         var m = e.ChatMessage;
//         var parts = _sfxRegex.Split(message);
//         var badgesString = string.Join(", ", e.ChatMessage.Badges.Select(b => b.Key + " = " + b.Value));

//         if (parts.Length == 1)
//         {
//             _logger.Information($"Username: {m.Username}; User ID: {m.UserId}; Voice: {voice}; Priority: {priority}; Message: {message}; Month: {m.SubscribedMonthCount}; Reward Id: {m.CustomRewardId}; {badgesString}");
//             _player.Add(new TTSMessage()
//             {
//                 Voice = voice,
//                 Message = message,
//                 Timestamp = DateTime.UtcNow,
//                 Username = m.Username,
//                 //Bits = m.Bits,
//                 Badges = e.Badges,
//                 Priority = priority
//             });
//             return;
//         }

//         var sfxMatches = _sfxRegex.Matches(message);
//         var sfxStart = sfxMatches.FirstOrDefault()?.Index ?? message.Length;

//         for (var i = 0; i < sfxMatches.Count; i++)
//         {
//             var sfxMatch = sfxMatches[i];
//             var sfxName = sfxMatch.Groups[1]?.ToString()?.ToLower();

//             if (!File.Exists("sfx/" + sfxName + ".mp3"))
//             {
//                 parts[i * 2 + 2] = parts[i * 2] + " (" + parts[i * 2 + 1] + ")" + parts[i * 2 + 2];
//                 continue;
//             }

//             if (!string.IsNullOrWhiteSpace(parts[i * 2]))
//             {
//                 _logger.Information($"Username: {m.Username}; User ID: {m.UserId}; Voice: {voice}; Priority: {priority}; Message: {parts[i * 2]}; Month: {m.SubscribedMonthCount}; {badgesString}");
//                 _player.Add(new TTSMessage()
//                 {
//                     Voice = voice,
//                     Message = parts[i * 2],
//                     Moderator = m.IsModerator,
//                     Timestamp = DateTime.UtcNow,
//                     Username = m.Username,
//                     Bits = m.Bits,
//                     Badges = m.Badges,
//                     Priority = priority
//                 });
//             }

//             _logger.Information($"Username: {m.Username}; User ID: {m.UserId}; Voice: {voice}; Priority: {priority}; SFX: {sfxName}; Month: {m.SubscribedMonthCount}; {badgesString}");
//             _player.Add(new TTSMessage()
//             {
//                 Voice = voice,
//                 Message = sfxName,
//                 File = $"sfx/{sfxName}.mp3",
//                 Moderator = m.IsModerator,
//                 Timestamp = DateTime.UtcNow,
//                 Username = m.Username,
//                 Bits = m.Bits,
//                 Badges = m.Badges,
//                 Priority = priority
//             });
//         }

//         if (!string.IsNullOrWhiteSpace(parts.Last()))
//         {
//             _logger.Information($"Username: {m.Username}; User ID: {m.UserId}; Voice: {voice}; Priority: {priority}; Message: {parts.Last()}; Month: {m.SubscribedMonthCount}; {badgesString}");
//             _player.Add(new TTSMessage()
//             {
//                 Voice = voice,
//                 Message = parts.Last(),
//                 Moderator = m.IsModerator,
//                 Timestamp = DateTime.UtcNow,
//                 Username = m.Username,
//                 Bits = m.Bits,
//                 Badges = m.Badges,
//                 Priority = priority
//             });
//         }
//     }
// }