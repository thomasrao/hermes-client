using Microsoft.Extensions.Hosting;
using Serilog;
using TwitchChatTTS.Chat.Speech;

namespace TwitchChatTTS
{
    public class TTSEngine : IHostedService
    {
        private readonly AudioPlaybackEngine _playback;
        private readonly TTSPlayer _player;
        private readonly ILogger _logger;

        public CancellationTokenSource? PlayerSource;


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
                PlayerSource = new CancellationTokenSource();
                while (true)
                {
                    try
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            _logger.Warning("TTS Engine - Cancellation requested.");
                            return;
                        }
                        if (_player.IsEmpty())
                        {
                            try
                            {
                                PlayerSource.Token.WaitHandle.WaitOne();
                            }
                            catch (Exception) { }
                        }

                        while (_player.Playing != null)
                        {
                            await Task.Delay(100);
                        }

                        PlayerSource = new CancellationTokenSource();
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