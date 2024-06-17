using TwitchChatTTS.Chat.Commands.Parameters;
using TwitchLib.Client.Models;

namespace TwitchChatTTS.Chat.Commands
{
    public abstract class ChatCommand
    {
        public string Name { get; }
        public string Description { get; }
        public IList<ChatCommandParameter> Parameters { get => _parameters.AsReadOnly(); }
        private IList<ChatCommandParameter> _parameters;

        public ChatCommand(string name, string description)
        {
            Name = name;
            Description = description;
            _parameters = new List<ChatCommandParameter>();
        }

        protected void AddParameter(ChatCommandParameter parameter, bool optional = false)
        {
            if (parameter != null && parameter.Clone() is ChatCommandParameter p) {
                _parameters.Add(optional ? p.Permissive() : p);
            }
        }

        public abstract Task<bool> CheckPermissions(ChatMessage message, long broadcasterId);
        public abstract Task Execute(IList<string> args, ChatMessage message, long broadcasterId);
    }
}