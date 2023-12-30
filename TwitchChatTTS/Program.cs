using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using NAudio.Wave;
using TwitchLib.Api;
using TwitchLib.Client;
using TwitchLib.Client.Events;
using TwitchLib.Client.Models;
using TwitchLib.Communication.Clients;
using TwitchLib.Communication.Events;
using TwitchLib.PubSub;
using TwitchLib.PubSub.Events;
using NAudio.Wave.SampleProviders;

/**
Future handshake/connection procedure:
- GET all tts config data
- Continuous connection to server to receive commands from tom & send logs/errors (med priority, though tough task)

Ideas:
- Filter messages by badges, username, ..., etc.
- Filter messages by content.
- Speed up TTS based on message queue size?
- Cut TTS off shortly after raid (based on size of raid)?
- Limit duration of TTS
- Voice selection for channel and per user.
**/

// dotnet publish -r linux-x64 -p:PublishSingleFile=true --self-contained true
// dotnet publish -r win-x64 -p:PublishSingleFile=true --self-contained true
// SE voices: https://api.streamelements.com/kappa/v2/speech?voice=brian&text=hello

// Read redeems from file.
var redeems = File.Exists(".redeems") ? await File.ReadAllLinesAsync(".redeems") : new string[0];

// Fetch id and username based on api key given.
HermesClient hermes = new HermesClient();
Console.WriteLine("Fetching Hermes account details...");
await hermes.UpdateHermesAccount();

Console.WriteLine("ID: " + hermes.Id);
Console.WriteLine("Username: " + hermes.Username);
Console.WriteLine();

Console.WriteLine("Fetching Twitch API details from Hermes...");
TwitchApiClient twitchapiclient = new TwitchApiClient(await hermes.FetchTwitchBotToken());
await twitchapiclient.Authorize();

var sfxRegex = new Regex(@"\(([A-Za-z0-9_-]+)\)");
var voiceRegex = new Regex(@"\b(Filiz|Astrid|Tatyana|Maxim|Carmen|Ines|Cristiano|Vitoria|Ricardo|Maja|Jan|Jacek|Ewa|Ruben|Lotte|Liv|Seoyeon|Takumi|Mizuki|Giorgio|Carla|Bianca|Karl|Dora|Mathieu|Celine|Chantal|Penelope|Miguel|Mia|Enrique|Conchita|Geraint|Salli|Matthew|Kimberly|Kendra|Justin|Joey|Joanna|Ivy|Raveena|Aditi|Emma|Brian|Amy|Russell|Nicole|Vicki|Marlene|Hans|Naja|Mads|Gwyneth|Zhiyu|Tracy|Danny|Huihui|Yaoyao|Kangkang|HanHan|Zhiwei|Asaf|An|Stefanos|Filip|Ivan|Heidi|Herena|Kalpana|Hemant|Matej|Andika|Rizwan|Lado|Valluvar|Linda|Heather|Sean|Michael|Karsten|Guillaume|Pattara|Jakub|Szabolcs|Hoda|Naayf)\:(.*?)(?=\Z|\b(?:Filiz|Astrid|Tatyana|Maxim|Carmen|Ines|Cristiano|Vitoria|Ricardo|Maja|Jan|Jacek|Ewa|Ruben|Lotte|Liv|Seoyeon|Takumi|Mizuki|Giorgio|Carla|Bianca|Karl|Dora|Mathieu|Celine|Chantal|Penelope|Miguel|Mia|Enrique|Conchita|Geraint|Salli|Matthew|Kimberly|Kendra|Justin|Joey|Joanna|Ivy|Raveena|Aditi|Emma|Brian|Amy|Russell|Nicole|Vicki|Marlene|Hans|Naja|Mads|Gwyneth|Zhiyu|Tracy|Danny|Huihui|Yaoyao|Kangkang|HanHan|Zhiwei|Asaf|An|Stefanos|Filip|Ivan|Heidi|Herena|Kalpana|Hemant|Matej|Andika|Rizwan|Lado|Valluvar|Linda|Heather|Sean|Michael|Karsten|Guillaume|Pattara|Jakub|Szabolcs|Hoda|Naayf)\:)", RegexOptions.IgnoreCase);

TTSPlayer player = new TTSPlayer();
ISampleProvider playing = null;

var channels = File.Exists(".twitchchannels") ? File.ReadAllLines(".twitchchannels") : new string[] { hermes.Username };
twitchapiclient.InitializeClient(hermes, channels);
twitchapiclient.InitializePublisher(player, redeems);

void HandleMessage(int priority, string voice, string message, OnMessageReceivedArgs e, bool bot) {
    var m = e.ChatMessage;
    var parts = sfxRegex.Split(message);
    var sfxMatches = sfxRegex.Matches(message);
    var sfxStart = sfxMatches.FirstOrDefault()?.Index ?? message.Length;
    var alphanumeric = new Regex(@"[^a-zA-Z0-9!@#$%&\^*+\-_(),+':;?.,\[\]\s\\/~`]");
    message = alphanumeric.Replace(message, " ");

    if (string.IsNullOrWhiteSpace(message)) {
        return;
    }

    if (parts.Length == 1) {
        Console.WriteLine($"Voice: {voice}; Priority: {priority}; Message: {message}; Month: {m.SubscribedMonthCount}; {string.Join(", ", e.ChatMessage.Badges.Select(b => b.Key + " = " + b.Value))}");
        player.Add(new TTSMessage() {
            Voice = voice,
            Bot = bot,
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

    for (var i = 0; i < sfxMatches.Count; i++) {
        var sfxMatch = sfxMatches[i];
        var sfxName = sfxMatch.Groups[1]?.ToString()?.ToLower();

        if (!File.Exists("sfx/" + sfxName + ".mp3")) {
            parts[i * 2 + 2] = parts[i * 2] + " (" + parts[i * 2 + 1] + ")" + parts[i * 2 + 2];
            continue;
        }

        if (!string.IsNullOrWhiteSpace(parts[i * 2])) {
            Console.WriteLine($"Voice: {voice}; Priority: {priority}; Message: {parts[i * 2]}; Month: {m.SubscribedMonthCount}; {string.Join(", ", e.ChatMessage.Badges.Select(b => b.Key + " = " + b.Value))}");
            player.Add(new TTSMessage() {
                Voice = voice,
                Bot = bot,
                Message = parts[i * 2],
                Moderator = m.IsModerator,
                Timestamp = DateTime.UtcNow,
                Username = m.Username,
                Bits = m.Bits,
                Badges = m.Badges,
                Priority = priority
            });
        }

        Console.WriteLine($"Voice: {voice}; Priority: {priority}; SFX: {sfxName}; Month: {m.SubscribedMonthCount}; {string.Join(", ", e.ChatMessage.Badges.Select(b => b.Key + " = " + b.Value))}");
        player.Add(new TTSMessage() {
            Voice = voice,
            Bot = bot,
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
        Console.WriteLine($"Voice: {voice}; Priority: {priority}; Message: {parts.Last()}; Month: {m.SubscribedMonthCount}; {string.Join(", ", e.ChatMessage.Badges.Select(b => b.Key + " = " + b.Value))}");
        player.Add(new TTSMessage() {
            Voice = voice,
            Bot = bot,
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

twitchapiclient.AddOnNewMessageReceived(async Task (object? s, OnMessageReceivedArgs e) => {
    var m = e.ChatMessage;
    var msg = e.ChatMessage.Message;
    if ((m.IsVip || m.IsModerator || m.IsBroadcaster) && (msg.ToLower().StartsWith("!skip ") || msg.ToLower() == "!skip")) {
        AudioPlaybackEngine.Instance.RemoveMixerInput(playing);
        playing = null;
        return;
    }

    string[] bots = new string[] { "nightbot", "streamelements", "own3d", "streamlabs", "soundalerts", "pokemoncommunitygame" };
    bool bot = bots.Any(b => b == m.Username);
    if (bot || m.IsBroadcaster || msg.StartsWith('!')) {
        return;
    }
    
    string[] bad = new string[] { "incel", "simp", "virgin", "faggot", "fagg", "fag", "nigger", "nigga", "nigg", "nig", "whore", "retard", "cock", "fuck", "bastard", "wanker", "bollocks", "motherfucker", "bitch", "bish", "bich", "asshole", "ass", "dick", "dickhead", "frigger", "shit", "slut", "turd", "twat", "nigra", "penis" };
    foreach (var b in bad) {
        msg = new Regex($@"\b{b}\b", RegexOptions.IgnoreCase).Replace(msg, "");
    }

    msg = new Regex(@"%").Replace(msg, " percent ");
    msg = new Regex(@"https?\:\/\/[-a-zA-Z0-9@:%._\+~#=]{1,256}\.[a-zA-Z0-9()]{1,6}\b(?:[-a-zA-Z0-9()@:%_\+.~#?&\/\/=]*)").Replace(msg, "");
    msg = new Regex(@"\bfreeze153").Replace(msg, "");

    // Filter highly repetitive words (like emotes) from message.
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
    
    foreach (var w in words) {
        if (wordCounter.ContainsKey(w)) {
            wordCounter[w]++;
        } else {
            wordCounter.Add(w, 1);
        }
    }

    int priority = 0;
    if (m.IsStaff) {
        priority = int.MinValue;
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

    var matches = voiceRegex.Matches(msg);
    int defaultEnd = matches.FirstOrDefault()?.Index ?? msg.Length;
    if (defaultEnd > 0) {
        HandleMessage(priority, "Brian", msg.Substring(0, defaultEnd), e, bot);
    }

    foreach (Match match in matches) {
        var message = match.Groups[2].ToString();
        if (string.IsNullOrWhiteSpace(message)) {
            continue;
        }

        var voice = match.Groups[1].ToString();
        voice = voice[0].ToString().ToUpper() + voice.Substring(1).ToLower();

        HandleMessage(priority, voice, message, e, bot);
    }
});

AudioPlaybackEngine.Instance.AddOnMixerInputEnded((object? s, SampleProviderEventArgs e) => {
    if (e.SampleProvider == playing) {
        playing = null;
    }
});

Task.Run(async () => {
    while (true) {
        try {
            var m = player.ReceiveBuffer();
            if (m == null) {
                await Task.Delay(200);
                continue;
            }

            string url = $"https://api.streamelements.com/kappa/v2/speech?voice={m.Voice}&text={m.Message}";
            var sound = new NetworkWavSound(url);
            var provider = new CachedWavProvider(sound);
            var data = AudioPlaybackEngine.Instance.ConvertSound(provider);

            m.Audio = data;
            player.Ready(m);
        } catch (COMException e) {
            Console.WriteLine(e.GetType().Name + ": " + e.Message + " (HResult: " + e.HResult + ")");
        } catch (Exception e) {
            Console.WriteLine(e.GetType().Name + ": " + e.Message);
        }
    }
});

Task.Run(async () => {
    while (true) {
        try {
            while (player.IsEmpty() || playing != null) {
                await Task.Delay(200);
            }
            var m = player.ReceiveReady();
            if (m == null) {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(m.File) && File.Exists(m.File)) {
                Console.WriteLine("Playing sfx: " + m.File);
                AudioPlaybackEngine.Instance.PlaySound(m.File);
                continue;
            }

            Console.WriteLine("Playing message: " + m.Message);
            playing = m.Audio;
            AudioPlaybackEngine.Instance.AddMixerInput(m.Audio);
        } catch (Exception e) {
            Console.WriteLine(e.GetType().Name + ": " + e.Message);
        }
    }
});

Console.WriteLine("Twitch API client connecting...");
twitchapiclient.Connect();
Console.ReadLine();
Console.ReadLine();