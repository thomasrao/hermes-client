namespace TwitchChatTTS.Chat.Commands
{
    public enum ChatCommandResult
    {
        Unknown = 0,
        Missing = 1,
        Success = 2,
        Permission = 3,
        Syntax = 4,
        Fail = 5,
        OtherRoom = 6,
    }
}