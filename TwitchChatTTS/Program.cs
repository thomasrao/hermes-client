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

Console.WriteLine("Username: " + hermes.Username);
Console.WriteLine();

Console.WriteLine("Fetching Twitch API details from Hermes...");
TwitchApiClient twitchapiclient = new TwitchApiClient(await hermes.FetchTwitchBotToken());
await twitchapiclient.Authorize();

Console.WriteLine("Fetching TTS username filters...");
var usernameFilters = (await hermes.FetchTTSUsernameFilters())
                            .ToDictionary(x => x.username, x => x);
Console.WriteLine($"{usernameFilters.Where(f => f.Value.tag == "blacklisted").Count()} username(s) have been blocked.");
Console.WriteLine($"{usernameFilters.Where(f => f.Value.tag == "priority").Count()} user(s) have been prioritized.");

var enabledVoices = await hermes.FetchTTSEnabledVoices();
Console.WriteLine($"{enabledVoices.Count()} TTS voices enabled.");

var wordFilters = await hermes.FetchTTSWordFilters();
Console.WriteLine($"{wordFilters.Count()} TTS word filters.");

var defaultVoice = await hermes.FetchTTSDefaultVoice();
Console.WriteLine("Default Voice: " + defaultVoice);

TTSPlayer player = new TTSPlayer();
ISampleProvider playing = null;

var handler = new ChatMessageHandler(player, defaultVoice, enabledVoices, usernameFilters, wordFilters);

var channels = File.Exists(".twitchchannels") ? File.ReadAllLines(".twitchchannels") : new string[] { hermes.Username };
Console.WriteLine("Twitch channels: " + string.Join(", ", channels));
twitchapiclient.InitializeClient(hermes, channels);
twitchapiclient.InitializePublisher(player, redeems);


twitchapiclient.AddOnNewMessageReceived(async Task (object? s, OnMessageReceivedArgs e) => {
    var result = handler.Handle(e);

    switch (result) {
        case MessageResult.Skip:
            AudioPlaybackEngine.Instance.RemoveMixerInput(playing);
            playing = null;
            break;
        default:
            break;
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
            var resampled = new WdlResamplingSampleProvider(data, AudioPlaybackEngine.Instance.SampleRate);

            m.Audio = resampled;
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