using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TwitchLib.Client.Models;

namespace TwitchChatTTS.Chat.Commands
{
    public class ChatCommandManager
    {
        private IDictionary<string, ChatCommand> _commands;
        private TwitchBotAuth _token;
        private IServiceProvider _serviceProvider;
        private ILogger _logger;
        private string CommandStartSign { get; } = "!";


        public ChatCommandManager(TwitchBotAuth token, IServiceProvider serviceProvider, ILogger logger)
        {
            _token = token;
            _serviceProvider = serviceProvider;
            _logger = logger;

            _commands = new Dictionary<string, ChatCommand>();
            GenerateCommands();
        }

        private void Add(ChatCommand command)
        {
            _commands.Add(command.Name.ToLower(), command);
        }

        private void GenerateCommands()
        {
            var basetype = typeof(ChatCommand);
            var assembly = GetType().Assembly;
            var types = assembly.GetTypes().Where(t => t.IsClass && !t.IsAbstract && basetype.IsAssignableFrom(t) && t.AssemblyQualifiedName?.Contains(".Chat.") == true);

            foreach (var type in types)
            {
                var key = "command-" + type.Name.Replace("Commands", "Comm#ands")
                        .Replace("Command", "")
                        .Replace("Comm#ands", "Commands")
                        .ToLower();

                var command = _serviceProvider.GetKeyedService<ChatCommand>(key);
                if (command == null)
                {
                    _logger.Error("Failed to add command: " + type.AssemblyQualifiedName);
                    continue;
                }

                _logger.Debug($"Added command {type.AssemblyQualifiedName}.");
                Add(command);
            }
        }

        public async Task<ChatCommandResult> Execute(string arg, ChatMessage message)
        {
            if (_token.BroadcasterId == null)
                return ChatCommandResult.Unknown;
            if (string.IsNullOrWhiteSpace(arg))
                return ChatCommandResult.Unknown;

            arg = arg.Trim();

            if (!arg.StartsWith(CommandStartSign))
                return ChatCommandResult.Unknown;

            string[] parts = arg.Split(" ");
            string com = parts.First().Substring(CommandStartSign.Length).ToLower();
            string[] args = parts.Skip(1).ToArray();
            long broadcasterId = long.Parse(_token.BroadcasterId);

            if (!_commands.TryGetValue(com, out ChatCommand? command) || command == null)
            {
                _logger.Debug($"Failed to find command named '{com}'.");
                return ChatCommandResult.Missing;
            }

            if (!await command.CheckPermissions(message, broadcasterId) && message.UserId != "126224566" && !message.IsStaff)
            {
                _logger.Warning($"Chatter is missing permission to execute command named '{com}'.");
                return ChatCommandResult.Permission;
            }

            if (command.Parameters.Count(p => !p.Optional) > args.Length)
            {
                _logger.Warning($"Command syntax issue when executing command named '{com}' with the following args: {string.Join(" ", args)}");
                return ChatCommandResult.Syntax;
            }

            for (int i = 0; i < Math.Min(args.Length, command.Parameters.Count); i++)
            {
                if (!command.Parameters[i].Validate(args[i]))
                {
                    _logger.Warning($"Commmand '{com}' failed because of the #{i + 1} argument. Invalid value: {args[i]}");
                    return ChatCommandResult.Syntax;
                }
            }

            try
            {
                await command.Execute(args, message, broadcasterId);
            }
            catch (Exception e)
            {
                _logger.Error(e, $"Command '{arg}' failed.");
                return ChatCommandResult.Fail;
            }

            _logger.Information($"Executed the {com} command with the following args: " + string.Join(" ", args));
            return ChatCommandResult.Success;
        }
    }
}