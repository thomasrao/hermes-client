using Serilog;
using static TwitchChatTTS.Chat.Commands.TTSCommands;

namespace TwitchChatTTS.Chat.Commands
{
    public class CommandFactory : ICommandFactory
    {
        private readonly IEnumerable<IChatCommand> _commands;
        private readonly ICommandBuilder _builder;
        private readonly ILogger _logger;

        public CommandFactory(
            IEnumerable<IChatCommand> commands,
            ICommandBuilder builder,
            ILogger logger
        )
        {
            _commands = commands;
            _builder = builder;
            _logger = logger;
        }

        public ICommandSelector Build()
        {
            foreach (var command in _commands)
            {
                try
                {
                    _logger.Debug($"Creating command tree for '{command.Name}'.");
                    command.Build(_builder);
                }
                catch (Exception e)
                {
                    _logger.Error(e, $"Failed to properly load a chat command [command name: {command.Name}]");
                }
            }

            return _builder.Build();
        }
    }
}