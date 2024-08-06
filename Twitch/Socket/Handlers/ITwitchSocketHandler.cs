namespace TwitchChatTTS.Twitch.Socket.Handlers
{
    public interface ITwitchSocketHandler
    {
        string Name { get; }
        Task Execute(TwitchWebsocketClient sender, object data);
    }
}