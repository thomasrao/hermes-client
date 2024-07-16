using TwitchChatTTS.Chat.Commands.Parameters;
using TwitchChatTTS.Hermes.Socket;
using TwitchLib.Client.Models;

namespace TwitchChatTTS.Chat.Commands
{
    public abstract class ChatCommand
    {
        public string Name { get; }
        public string Description { get; }
        public IList<ChatCommandParameter> Parameters { get => _parameters.AsReadOnly(); }
        public bool DefaultPermissionsOverwrite { get; }

        private IList<ChatCommandParameter> _parameters;

        public ChatCommand(string name, string description)
        {
            Name = name;
            Description = description;
            DefaultPermissionsOverwrite = false;
            _parameters = new List<ChatCommandParameter>();
        }

        protected void AddParameter(ChatCommandParameter parameter, bool optional = false)
        {
            if (parameter != null && parameter.Clone() is ChatCommandParameter p) {
                _parameters.Add(optional ? p.Permissive() : p);
            }
        }

        public abstract Task<bool> CheckDefaultPermissions(ChatMessage message);
        public abstract Task Execute(IList<string> args, ChatMessage message, HermesSocketClient client);
    }
}