using System.Runtime.InteropServices;
using System.Web;
using Microsoft.Extensions.Hosting;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Serilog;
using TwitchChatTTS.Chat.Speech;

namespace TwitchChatTTS
{
    public class TTSEngine : IHostedService
    {
        private readonly AudioPlaybackEngine _playback;
        private readonly TTSPlayer _player;
        private readonly ILogger _logger;


        public TTSEngine(AudioPlaybackEngine playback, TTSPlayer player, ILogger logger)
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
                            _logger.Warning("TTS Engine - Cancellation requested.");
                            return;
                        }
                        while (_player.IsEmpty() || _player.Playing != null)
                        {
                            await Task.Delay(200, cancellationToken);
                            continue;
                        }

                        var messageData = _player.ReceiveReady();
                        if (messageData == null)
                            continue;

                        if (messageData.Audio != null)
                        {
                            _player.Playing = messageData;
                            _playback.AddMixerInput(messageData.Audio);
                            string message = string.Join(" ", messageData.Messages.Select(m => m.File == null ? m.Message : '(' + m.File + ')'));
                            _logger.Debug($"Playing TTS message [message: {message}][chatter id: {messageData.ChatterId}][priority: {messageData.Priority}][message id: {messageData.MessageId}][broadcaster id: {messageData.RoomId}]");
                        }
                    }
                    catch (Exception e)
                    {
                        _logger.Error(e, "Failed to play a TTS audio message");
                    }
                }
            });
            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }
}