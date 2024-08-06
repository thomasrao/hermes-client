namespace TwitchChatTTS.Twitch.Socket.Handlers
{
    public class SessionKeepAliveHandler : ITwitchSocketHandler
    {
        public string Name => "session_keepalive";

        public Task Execute(TwitchWebsocketClient sender, object data)
        {
            return Task.CompletedTask;
        }
    }
}