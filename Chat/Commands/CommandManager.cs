using System.Text.RegularExpressions;
using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TwitchChatTTS.Chat.Commands.Limits;
using TwitchChatTTS.Chat.Groups.Permissions;
using TwitchChatTTS.Hermes.Socket;
using TwitchChatTTS.Twitch.Socket.Messages;
using static TwitchChatTTS.Chat.Commands.TTSCommands;

namespace TwitchChatTTS.Chat.Commands
{
    public class CommandManager : ICommandManager
    {
        private readonly User _user;
        private ICommandSelector _commandSelector;
        private readonly HermesSocketClient _hermes;
        //private readonly TwitchWebsocketClient _twitch;
        private readonly IGroupPermissionManager _permissionManager;
        private readonly IUsagePolicy<long> _permissionPolicy;
        private readonly ILogger _logger;
        private string CommandStartSign { get; } = "!";


        public CommandManager(
            User user,
            [FromKeyedServices("hermes")] SocketClient<WebSocketMessage> hermes,
            //[FromKeyedServices("twitch")] SocketClient<TwitchWebsocketMessage> twitch,
            IGroupPermissionManager permissionManager,
            IUsagePolicy<long> limitManager,
            ILogger logger
        )
        {
            _user = user;
            _hermes = (hermes as HermesSocketClient)!;
            //_twitch = (twitch as TwitchWebsocketClient)!;
            _permissionManager = permissionManager;
            _permissionPolicy = limitManager;
            _logger = logger;
        }


        public async Task<ChatCommandResult> Execute(string arg, ChannelChatMessage message, IEnumerable<string> groups)
        {
            if (string.IsNullOrWhiteSpace(arg))
                return ChatCommandResult.Unknown;

            arg = arg.Trim();

            if (!arg.StartsWith(CommandStartSign))
                return ChatCommandResult.Unknown;

            if (message.BroadcasterUserId != _user.TwitchUserId.ToString())
                return ChatCommandResult.OtherRoom;

            string[] parts = Regex.Matches(arg.Substring(CommandStartSign.Length), "(?<match>[^\"\\n\\s]+|\"[^\"\\n]*\")")
                .Cast<Match>()
                .Select(m => m.Groups["match"].Value)
                .Where(m => !string.IsNullOrEmpty(m))
                .Select(m => m.StartsWith('"') && m.EndsWith('"') ? m.Substring(1, m.Length - 2) : m)
                .ToArray();
            string[] args = parts.ToArray();
            string com = args.First().ToLower();

            CommandSelectorResult selectorResult = _commandSelector.GetBestMatch(args, message.Message.Fragments);
            if (selectorResult.Command == null)
            {
                _logger.Warning($"Could not match '{arg}' to any command [chatter: {message.ChatterUserLogin}][chatter id: {message.ChatterUserId}]");
                return ChatCommandResult.Missing;
            }

            // Check if command can be executed by this chatter.
            var command = selectorResult.Command;
            long chatterId = long.Parse(message.ChatterUserId);
            var path = $"tts.commands.{com}";
            if (chatterId != _user.OwnerId)
            {
                bool executable = command.AcceptCustomPermission ? CanExecute(chatterId, groups, path, selectorResult.Permissions) : false;
                if (!executable)
                {
                    _logger.Warning($"Denied permission to use command [chatter id: {chatterId}][args: {arg}][command type: {command.GetType().Name}]");
                    return ChatCommandResult.Permission;
                }
            }

            if (!_permissionPolicy.TryUse(chatterId, groups, path))
            {
                _logger.Warning($"Chatter reached usage limit on command [command type: {command.GetType().Name}][chatter id: {chatterId}][path: {path}][groups: {string.Join("|", groups)}]");
                return ChatCommandResult.RateLimited;
            }

            // Check if the arguments are valid.
            var arguments = _commandSelector.GetNonStaticArguments(args, selectorResult.Path);
            foreach (var entry in arguments)
            {
                var parameter = entry.Value;
                var argument = entry.Key;
                // Optional parameters were validated while fetching this command.
                if (!parameter.Optional && !parameter.Validate(argument, message.Message.Fragments))
                {
                    _logger.Warning($"Command failed due to an argument being invalid [argument name: {parameter.Name}][argument value: {argument}][parameter type: {parameter.GetType().Name}][arguments: {arg}][command type: {command.GetType().Name}][chatter: {message.ChatterUserLogin}][chatter id: {message.ChatterUserId}]");
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
                _logger.Error(e, $"Command '{arg}' failed [args: {arg}][command type: {command.GetType().Name}][chatter: {message.ChatterUserLogin}][chatter id: {message.ChatterUserId}]");
                return ChatCommandResult.Fail;
            }

            _logger.Information($"Executed the {com} command [args: {arg}][command type: {command.GetType().Name}][chatter: {message.ChatterUserLogin}][chatter id: {message.ChatterUserId}]");
            return ChatCommandResult.Success;
        }

        public void Update(ICommandFactory factory)
        {
            _commandSelector = factory.Build();
        }

        private bool CanExecute(long chatterId, IEnumerable<string> groups, string path, string[]? additionalPaths)
        {
            _logger.Debug($"Checking for permission [chatter id: {chatterId}][group: {string.Join(", ", groups)}][path: {path}]{(additionalPaths != null ? "[paths: " + string.Join('|', additionalPaths) + "]" : string.Empty)}");
            if (_permissionManager.CheckIfAllowed(groups, path) == true)
            {
                if (additionalPaths == null)
                    return true;

                // All direct allow must not be false and at least one of them must be true.
                if (additionalPaths.All(p => _permissionManager.CheckIfDirectAllowed(groups, p) != false) && additionalPaths.Any(p => _permissionManager.CheckIfDirectAllowed(groups, p) == true))
                    return true;
            }
            return false;
        }
    }
}