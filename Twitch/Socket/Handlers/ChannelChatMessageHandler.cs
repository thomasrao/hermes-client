using Serilog;
using TwitchChatTTS.Chat.Messaging;
using TwitchChatTTS.Twitch.Socket.Messages;

namespace TwitchChatTTS.Twitch.Socket.Handlers
{
    public class ChannelChatMessageHandler : ITwitchSocketHandler
    {
        public string Name => "channel.chat.message";

        private readonly ChatMessageReader _reader;
        private readonly Configuration _configuration;
        private readonly ILogger _logger;


        public ChannelChatMessageHandler(
            ChatMessageReader reader,
            Configuration configuration,
            ILogger logger
        )
        {
            _reader = reader;
            _configuration = configuration;
            _logger = logger;
        }


        public async Task Execute(TwitchWebsocketClient sender, object data)
        {
            if (sender == null)
                return;
            if (data is not ChannelChatMessage message)
                return;

            await _reader.Execute(sender, message);
        }
    }
}