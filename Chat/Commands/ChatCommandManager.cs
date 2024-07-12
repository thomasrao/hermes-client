using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TwitchChatTTS.Chat.Groups;
using TwitchChatTTS.Chat.Groups.Permissions;
using TwitchLib.Client.Models;

namespace TwitchChatTTS.Chat.Commands
{
    public class ChatCommandManager
    {
        private IDictionary<string, ChatCommand> _commands;
        private readonly TwitchBotAuth _token;
        private readonly User _user;
        private readonly IGroupPermissionManager _permissionManager;
        private readonly IChatterGroupManager _chatterGroupManager;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger _logger;
        private string CommandStartSign { get; } = "!";


        public ChatCommandManager(
            TwitchBotAuth token,
            User user,
            IGroupPermissionManager permissionManager,
            IChatterGroupManager chatterGroupManager,
            IServiceProvider serviceProvider,
            ILogger logger
        )
        {
            _token = token;
            _user = user;
            _permissionManager = permissionManager;
            _chatterGroupManager = chatterGroupManager;
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

        public async Task<ChatCommandResult> Execute(string arg, ChatMessage message, IEnumerable<string> groups)
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
                _logger.Debug($"Failed to find command named '{com}' [args: {arg}][chatter: {message.Username}][chatter id: {message.UserId}]");
                return ChatCommandResult.Missing;
            }

            // Check if command can be executed by this chatter.
            long chatterId = long.Parse(message.UserId);
            if (chatterId != _user.OwnerId)
            {
                var executable = command.DefaultPermissionsOverwrite ? false : CanExecute(chatterId, groups, com);
                if (executable == false)
                {
                    _logger.Debug($"Denied permission to use command [chatter id: {chatterId}][command: {com}]");
                    return ChatCommandResult.Permission;
                }
                else if (executable == null && !await command.CheckDefaultPermissions(message, broadcasterId))
                {
                    _logger.Debug($"Chatter is missing default permission to execute command named '{com}' [args: {arg}][chatter: {message.Username}][chatter id: {message.UserId}]");
                    return ChatCommandResult.Permission;
                }
            }

            // Check if the syntax is correct.
            if (command.Parameters.Count(p => !p.Optional) > args.Length)
            {
                _logger.Debug($"Command syntax issue when executing command named '{com}' [args: {arg}][chatter: {message.Username}][chatter id: {message.UserId}]");
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
                _logger.Error(e, $"Command '{arg}' failed [args: {arg}][chatter: {message.Username}][chatter id: {message.UserId}]");
                return ChatCommandResult.Fail;
            }

            _logger.Information($"Executed the {com} command [args: {arg}][chatter: {message.Username}][chatter id: {message.UserId}]");
            return ChatCommandResult.Success;
        }

        private bool? CanExecute(long chatterId, IEnumerable<string> groups, string path)
        {
            _logger.Debug($"Checking for permission [chatter id: {chatterId}][group: {string.Join(", ", groups)}][path: {path}]");
            return _permissionManager.CheckIfAllowed(groups, path);
        }
    }
}