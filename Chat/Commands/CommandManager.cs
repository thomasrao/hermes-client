using System.Text.RegularExpressions;
using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TwitchChatTTS.Chat.Groups.Permissions;
using TwitchChatTTS.Hermes.Socket;
using TwitchLib.Client.Models;
using static TwitchChatTTS.Chat.Commands.TTSCommands;

namespace TwitchChatTTS.Chat.Commands
{
    public class CommandManager
    {
        private readonly User _user;
        private readonly ICommandSelector _commandSelector;
        private readonly HermesSocketClient _hermes;
        private readonly IGroupPermissionManager _permissionManager;
        private readonly ILogger _logger;
        private string CommandStartSign { get; } = "!";


        public CommandManager(
            IEnumerable<IChatCommand> commands,
            ICommandBuilder commandBuilder,
            User user,
            [FromKeyedServices("hermes")] SocketClient<WebSocketMessage> socketClient,
            IGroupPermissionManager permissionManager,
            ILogger logger
        )
        {
            _user = user;
            _hermes = (socketClient as HermesSocketClient)!;
            _permissionManager = permissionManager;
            _logger = logger;

            foreach (var command in commands)
            {
                _logger.Debug($"Creating command tree for '{command.Name}'.");
                command.Build(commandBuilder);
            }

            _commandSelector = commandBuilder.Build();
        }


        public async Task<ChatCommandResult> Execute(string arg, ChatMessage message, IEnumerable<string> groups)
        {
            if (string.IsNullOrWhiteSpace(arg))
                return ChatCommandResult.Unknown;

            arg = arg.Trim();

            if (!arg.StartsWith(CommandStartSign))
                return ChatCommandResult.Unknown;

            string[] parts = Regex.Matches(arg.Substring(CommandStartSign.Length), "(?<match>[^\"\\n\\s]+|\"[^\"\\n]*\")")
                .Cast<Match>()
                .Select(m => m.Groups["match"].Value)
                .Select(m => m.StartsWith('"') && m.EndsWith('"') ? m.Substring(1, m.Length - 2) : m)
                .ToArray();
            string[] args = parts.ToArray();
            string com = args.First().ToLower();

            CommandSelectorResult selectorResult = _commandSelector.GetBestMatch(args);
            if (selectorResult.Command == null)
            {
                _logger.Warning($"Could not match '{arg}' to any command.");
                return ChatCommandResult.Missing;
            }

            // Check if command can be executed by this chatter.
            var command = selectorResult.Command;
            long chatterId = long.Parse(message.UserId);
            if (chatterId != _user.OwnerId)
            {
                var executable = command.AcceptCustomPermission ? CanExecute(chatterId, groups, com) : null;
                if (executable == false)
                {
                    _logger.Debug($"Denied permission to use command [chatter id: {chatterId}][command: {com}]");
                    return ChatCommandResult.Permission;
                }
                else if (executable == null && !command.CheckDefaultPermissions(message))
                {
                    _logger.Debug($"Chatter is missing default permission to execute command named '{com}' [args: {arg}][command type: {command.GetType().Name}][chatter: {message.Username}][chatter id: {message.UserId}]");
                    return ChatCommandResult.Permission;
                }
            }

            // Check if the arguments are correct.
            var arguments = _commandSelector.GetNonStaticArguments(args, selectorResult.Path);
            foreach (var entry in arguments)
            {
                var parameter = entry.Value;
                var argument = entry.Key;
                if (!parameter.Validate(argument))
                {
                    _logger.Warning($"Command failed due to an argument being invalid [argument name: {parameter.Name}][argument value: {argument}][arguments: {arg}][command type: {command.GetType().Name}][chatter: {message.Username}][chatter id: {message.UserId}]");
                    return ChatCommandResult.Syntax;
                }
            }

            var values = arguments.ToDictionary(d => d.Value.Name, d => d.Key);
            try
            {
                await command.Execute(values, message, _hermes);
            }
            catch (Exception e)
            {
                _logger.Error(e, $"Command '{arg}' failed [args: {arg}][command type: {command.GetType().Name}][chatter: {message.Username}][chatter id: {message.UserId}]");
                return ChatCommandResult.Fail;
            }

            _logger.Information($"Executed the {com} command [args: {arg}][command type: {command.GetType().Name}][chatter: {message.Username}][chatter id: {message.UserId}]");
            return ChatCommandResult.Success;
        }

        private bool? CanExecute(long chatterId, IEnumerable<string> groups, string path)
        {
            _logger.Debug($"Checking for permission [chatter id: {chatterId}][group: {string.Join(", ", groups)}][path: {path}]");
            return _permissionManager.CheckIfAllowed(groups, path);
        }
    }
}