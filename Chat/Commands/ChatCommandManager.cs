using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TwitchLib.Client.Models;

namespace TwitchChatTTS.Chat.Commands
{
    public class ChatCommandManager
    {
        private IDictionary<string, ChatCommand> _commands;
        private readonly TwitchBotAuth _token;
        private readonly User _user;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger _logger;
        private string CommandStartSign { get; } = "!";


        public ChatCommandManager(TwitchBotAuth token, User user, IServiceProvider serviceProvider, ILogger logger)
        {
            _token = token;
            _user = user;
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
                    _logger.Error("Failed to add chat command: " + type.AssemblyQualifiedName);
                    continue;
                }

                _logger.Debug($"Added chat command {type.AssemblyQualifiedName}");
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

            string[] parts = Regex.Matches(arg, "(?<match>[^\"\\n\\s]+|\"[^\"\\n]*\")")
                .Cast<Match>()
                .Select(m => m.Groups["match"].Value)
                .Select(m => m.StartsWith('"') && m.EndsWith('"') ? m.Substring(1, m.Length - 2) : m)
                .ToArray();
            string com = parts.First().Substring(CommandStartSign.Length).ToLower();
            string[] args = parts.Skip(1).ToArray();
            long broadcasterId = long.Parse(_token.BroadcasterId);

            if (!_commands.TryGetValue(com, out ChatCommand? command) || command == null)
            {
                // Could be for another bot or just misspelled.
                _logger.Debug($"Failed to find command named '{com}' [args: {arg}][chatter: {message.Username}][cid: {message.UserId}]");
                return ChatCommandResult.Missing;
            }

            if (!await command.CheckPermissions(message, broadcasterId) && message.UserId != _user.OwnerId?.ToString() && !message.IsStaff)
            {
                _logger.Warning($"Chatter is missing permission to execute command named '{com}' [args: {arg}][chatter: {message.Username}][cid: {message.UserId}]");
                return ChatCommandResult.Permission;
            }

            if (command.Parameters.Count(p => !p.Optional) > args.Length)
            {
                _logger.Warning($"Command syntax issue when executing command named '{com}' [args: {arg}][chatter: {message.Username}][cid: {message.UserId}]");
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

            _logger.Information($"Executed the {com} command [arguments: {arg}]");
            return ChatCommandResult.Success;
        }
    }
}