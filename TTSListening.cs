using System.Runtime.InteropServices;
using System.Web;
using Microsoft.Extensions.Hosting;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Serilog;
using TwitchChatTTS.Chat.Speech;

namespace TwitchChatTTS
{
    public class TTSListening : IHostedService
    {
        private readonly AudioPlaybackEngine _playback;
        private readonly TTSPlayer _player;
        private readonly ILogger _logger;


        public TTSListening(AudioPlaybackEngine playback, TTSPlayer player, ILogger logger)
        {
            _playback = playback;
            _player = player;
            _logger = logger;
        }


        public Task StartAsync(CancellationToken cancellationToken)
        {
            Task.Run(async () =>
            {
                while (true)
                {
                    try
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            _logger.Warning("TTS Listening - Cancellation requested.");
                            return;
                        }

                        var group = _player.ReceiveBuffer();
                        if (group == null)
                        {
                            await Task.Delay(200, cancellationToken);
                            continue;
                        }

                        if (cancellationToken.IsCancellationRequested)
                        {
                            _logger.Warning("TTS Listening - Cancellation requested.");
                            return;
                        }

                        FetchMasterAudio(group);
                    }
                    catch (COMException e)
                    {
                        _logger.Error(e, "Failed to send request for TTS [HResult: " + e.HResult + "]");
                    }
                    catch (Exception e)
                    {
                        _logger.Error(e, "Failed to send request for TTS.");
                    }
                }
            });
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        private Task FetchMasterAudio(TTSGroupedMessage group)
        {
            return Task.Run(() =>
                {
                    var list = new List<ISampleProvider>();
                    foreach (var message in group.Messages)
                    {
                        if (string.IsNullOrEmpty(message.Message))
                        {
                            using (var reader = new AudioFileReader(message.File))
                            {
                                var data = _playback.ConvertSound(reader.ToWaveProvider());
                                var resampled = new WdlResamplingSampleProvider(data, _playback.SampleRate);
                                list.Add(resampled);
                            }
                            continue;
                        }
                        if (string.IsNullOrEmpty(message.Voice))
                        {
                            _logger.Error($"No voice has been selected for this message [message: {message.Message}]");
                            continue;
                        }

                        try
                        {
                            string url = $"https://api.streamelements.com/kappa/v2/speech?voice={message.Voice}&text={HttpUtility.UrlEncode(message.Message.Trim())}";
                            var nws = new NetworkWavSound(url);
                            var provider = new CachedWavProvider(nws);
                            var data = _playback.ConvertSound(provider);
                            var resampled = new WdlResamplingSampleProvider(data, _playback.SampleRate);
                            list.Add(resampled);
                        }
                        catch (Exception e)
                        {
                            _logger.Error(e, "Failed to fetch TTS message for ");
                        }
                    }

                    var merged = new ConcatenatingSampleProvider(list);
                    group.Audio = merged;
                    _player.Ready(group);
                });
        }
    }
}