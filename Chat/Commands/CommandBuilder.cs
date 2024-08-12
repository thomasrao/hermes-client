using Serilog;
using TwitchChatTTS.Chat.Commands.Parameters;
using TwitchChatTTS.Twitch.Socket.Messages;

namespace TwitchChatTTS.Chat.Commands
{
    public static class TTSCommands
    {
        public interface ICommandBuilder
        {
            ICommandSelector Build();
            ICommandBuilder AddPermission(string path);
            ICommandBuilder AddAlias(string alias, string child);
            void Clear();
            ICommandBuilder CreateCommandTree(string name, Action<ICommandBuilder> callback);
            ICommandBuilder CreateCommand(IChatPartialCommand command);
            ICommandBuilder CreateStaticInputParameter(string value, Action<ICommandBuilder> callback, bool optional = false);
            ICommandBuilder CreateMentionParameter(string name, bool enabled, bool optional = false);
            ICommandBuilder CreateObsTransformationParameter(string name, bool optional = false);
            ICommandBuilder CreateStateParameter(string name, bool optional = false);
            ICommandBuilder CreateUnvalidatedParameter(string name, bool optional = false);
            ICommandBuilder CreateVoiceNameParameter(string name, bool enabled, bool optional = false);

        }

        public sealed class CommandBuilder : ICommandBuilder
        {
            private CommandNode _root;
            private CommandNode _current;
            private Stack<CommandNode> _stack;
            private readonly User _user;
            private readonly ILogger _logger;

            public CommandBuilder(User user, ILogger logger)
            {
                _user = user;
                _logger = logger;

                _stack = new Stack<CommandNode>();
                _root = new CommandNode(new StaticParameter("root", "root"));
                _current = _root;
            }


            public ICommandBuilder AddPermission(string path)
            {
                if (_current == _root)
                    throw new Exception("Cannot add permissions without a command name.");

                _current.AddPermission(path);
                return this;
            }

            public ICommandBuilder AddAlias(string alias, string child)
            {
                if (_current == _root)
                    throw new Exception("Cannot add aliases without a command name.");
                if (_current.Children == null || !_current.Children.Any())
                    throw new Exception("Cannot add alias if this has no parameter.");

                _current.AddAlias(alias, child);
                return this;
            }

            public ICommandSelector Build()
            {
                return new CommandSelector(_root);
            }

            public void Clear()
            {
                _root = new CommandNode(new StaticParameter("root", "root"));
                ResetToRoot();
            }

            public ICommandBuilder CreateCommandTree(string name, Action<ICommandBuilder> callback)
            {
                ResetToRoot();

                var node = _current.CreateStaticInput(name);
                _logger.Debug($"Creating command name '{name}'");
                CreateStack(() =>
                {
                    _current = node;
                    callback(this);
                });
                return this;
            }

            public ICommandBuilder CreateCommand(IChatPartialCommand command)
            {
                if (_root == _current)
                    throw new Exception("Cannot create a command without a command name.");

                _current.CreateCommand(command);
                _logger.Debug($"Set command to '{command.GetType().Name}'");
                return this;
            }

            public ICommandBuilder CreateStaticInputParameter(string value, Action<ICommandBuilder> callback, bool optional = false)
            {
                if (_root == _current)
                    throw new Exception("Cannot create a parameter without a command name.");
                if (optional && _current.IsRequired() && _current.Command == null)
                    throw new Exception("Cannot create a optional parameter without giving the command to the last node with required parameter.");

                var node = _current.CreateStaticInput(value, optional);
                _logger.Debug($"Creating static parameter '{value}'");
                CreateStack(() =>
                {
                    _current = node;
                    callback(this);
                });
                return this;
            }

            public ICommandBuilder CreateMentionParameter(string name, bool enabled, bool optional = false)
            {
                if (_root == _current)
                    throw new Exception("Cannot create a parameter without a command name.");
                if (optional && _current.IsRequired() && _current.Command == null)
                    throw new Exception("Cannot create a optional parameter without giving the command to the last node with required parameter.");

                var node = _current.CreateUserInput(new MentionParameter(name, optional));
                _logger.Debug($"Creating obs transformation parameter '{name}'");
                _current = node;
                return this;
            }

            public ICommandBuilder CreateObsTransformationParameter(string name, bool optional = false)
            {
                if (_root == _current)
                    throw new Exception("Cannot create a parameter without a command name.");
                if (optional && _current.IsRequired() && _current.Command == null)
                    throw new Exception("Cannot create a optional parameter without giving the command to the last node with required parameter.");

                var node = _current.CreateUserInput(new OBSTransformationParameter(name, optional));
                _logger.Debug($"Creating obs transformation parameter '{name}'");
                _current = node;
                return this;
            }

            public ICommandBuilder CreateStateParameter(string name, bool optional = false)
            {
                if (_root == _current)
                    throw new Exception("Cannot create a parameter without a command name.");
                if (optional && _current.IsRequired() && _current.Command == null)
                    throw new Exception("Cannot create a optional parameter without giving the command to the last node with required parameter.");

                var node = _current.CreateUserInput(new StateParameter(name, optional));
                _logger.Debug($"Creating unvalidated parameter '{name}'");
                _current = node;
                return this;
            }

            public ICommandBuilder CreateUnvalidatedParameter(string name, bool optional = false)
            {
                if (_root == _current)
                    throw new Exception("Cannot create a parameter without a command name.");
                if (optional && _current.IsRequired() && _current.Command == null)
                    throw new Exception("Cannot create a optional parameter without giving the command to the last node with required parameter.");

                var node = _current.CreateUserInput(new UnvalidatedParameter(name, optional));
                _logger.Debug($"Creating unvalidated parameter '{name}'");
                _current = node;
                return this;
            }

            public ICommandBuilder CreateVoiceNameParameter(string name, bool enabled, bool optional = false)
            {
                if (_root == _current)
                    throw new Exception("Cannot create a parameter without a command name.");
                if (optional && _current.IsRequired() && _current.Command == null)
                    throw new Exception("Cannot create a optional parameter without giving the command to the last node with required parameter.");

                var node = _current.CreateUserInput(new TTSVoiceNameParameter(name, enabled, _user, optional));
                _logger.Debug($"Creating tts voice name parameter '{name}'");
                _current = node;
                return this;
            }

            private ICommandBuilder ResetToRoot()
            {
                _current = _root;
                _stack.Clear();
                return this;
            }

            private void CreateStack(Action func)
            {
                try
                {
                    _stack.Push(_current);
                    func();
                }
                finally
                {
                    _current = _stack.Pop();
                }
            }
        }

        public interface ICommandSelector
        {
            CommandSelectorResult GetBestMatch(string[] args, TwitchChatFragment[] fragments);
            IDictionary<string, CommandParameter> GetNonStaticArguments(string[] args, string path);
        }

        public sealed class CommandSelector : ICommandSelector
        {
            private CommandNode _root;

            public CommandSelector(CommandNode root)
            {
                _root = root;
            }

            public CommandSelectorResult GetBestMatch(string[] args, TwitchChatFragment[] fragments)
            {
                return GetBestMatch(_root, fragments, args, null, string.Empty, null);
            }

            private CommandSelectorResult GetBestMatch(CommandNode node, TwitchChatFragment[] fragments, IEnumerable<string> args, IChatPartialCommand? match, string path, string[]? permissions)
            {
                if (node == null || !args.Any())
                    return new CommandSelectorResult(match, path, permissions);
                if (!node.Children.Any())
                    return new CommandSelectorResult(node.Command ?? match, path, permissions);

                var argument = args.First();
                var argumentLower = argument.ToLower();
                foreach (var child in node.Children)
                {
                    var perms = child.Permissions != null ? (permissions ?? []).Union(child.Permissions).Distinct().ToArray() : permissions;
                    if (child.Parameter.GetType() == typeof(StaticParameter))
                    {
                        if (child.Parameter.Name.ToLower() == argumentLower)
                            return GetBestMatch(child, fragments, args.Skip(1), child.Command ?? match, (path.Length == 0 ? string.Empty : path + ".") + child.Parameter.Name.ToLower(), perms);
                        continue;
                    }
                    if ((!child.Parameter.Optional || child.Parameter.Validate(argument, fragments)) && child.Command != null)
                        return GetBestMatch(child, fragments, args.Skip(1), child.Command, (path.Length == 0 ? string.Empty : path + ".") + "*", perms);
                    if (!child.Parameter.Optional)
                        return GetBestMatch(child, fragments, args.Skip(1), match, (path.Length == 0 ? string.Empty : path + ".") + "*", permissions);
                }

                return new CommandSelectorResult(match, path, permissions);
            }

            public IDictionary<string, CommandParameter> GetNonStaticArguments(string[] args, string path)
            {
                Dictionary<string, CommandParameter> arguments = new Dictionary<string, CommandParameter>();
                CommandNode? current = _root;
                var parts = path.Split('.');
                if (args.Length < parts.Length)
                    throw new Exception($"Command path too long for the number of arguments passed in [path: {path}][parts: {parts.Length}][args count: {args.Length}]");

                for (var i = 0; i < parts.Length; i++)
                {
                    var part = parts[i];
                    if (part == "*")
                    {
                        current = current.Children.FirstOrDefault(n => n.Parameter.GetType() != typeof(StaticParameter));
                        if (current == null)
                            throw new Exception($"Cannot find command path [path: {path}][subpath: {part}]");

                        arguments.Add(args[i], current.Parameter);
                    }
                    else
                    {
                        current = current.Children.FirstOrDefault(n => n.Parameter.GetType() == typeof(StaticParameter) && n.Parameter.Name == part);
                        if (current == null)
                            throw new Exception($"Cannot find command path [path: {path}][subpath: {part}]");
                    }
                }

                return arguments;
            }
        }

        public class CommandSelectorResult
        {
            public IChatPartialCommand? Command { get; set; }
            public string Path { get; set; }
            public string[]? Permissions { get; set; }

            public CommandSelectorResult(IChatPartialCommand? command, string path, string[]? permissions)
            {
                Command = command;
                Path = path;
                Permissions = permissions;
            }
        }

        public class CommandValidationResult
        {
            public bool Result { get; set; }
            public string? ErrorParameterName { get; set; }

            public CommandValidationResult(bool result, string? parameterName)
            {
                Result = result;
                ErrorParameterName = parameterName;
            }
        }

        public sealed class CommandNode
        {
            public IChatPartialCommand? Command { get; private set; }
            public CommandParameter Parameter { get; }
            public string[]? Permissions { get; private set; }
            public IList<CommandNode> Children { get => _children.AsReadOnly(); }

            private IList<CommandNode> _children;

            public CommandNode(CommandParameter parameter)
            {
                Parameter = parameter;
                _children = new List<CommandNode>();
                Permissions = null;
            }


            public void AddPermission(string path)
            {
                if (Permissions == null)
                    Permissions = [path];
                else
                    Permissions = Permissions.Union([path]).ToArray();
            }

            public CommandNode AddAlias(string alias, string child)
            {
                var target = _children.FirstOrDefault(c => c.Parameter.Name == child);
                if (target == null)
                    throw new Exception($"Cannot find child parameter [parameter: {child}][alias: {alias}]");
                if (target.Parameter.GetType() != typeof(StaticParameter))
                    throw new Exception("Command aliases can only be used on static parameters.");
                if (Children.FirstOrDefault(n => n.Parameter.Name == alias) != null)
                    throw new Exception("Failed to create a command alias - name is already in use.");

                var clone = target.MemberwiseClone() as CommandNode;
                var node = new CommandNode(new StaticParameter(alias, alias, target.Parameter.Optional));
                node._children = target._children;
                node.Permissions = target.Permissions;
                node.Command = target.Command;
                _children.Add(node);
                return this;
            }

            public CommandNode CreateCommand(IChatPartialCommand command)
            {
                if (Command != null)
                    throw new InvalidOperationException("Cannot change the command of an existing one.");

                Command = command;
                return this;
            }

            public CommandNode CreateStaticInput(string value, bool optional = false)
            {
                if (Children.Any(n => n.Parameter.GetType() != typeof(StaticParameter)))
                    throw new InvalidOperationException("Cannot have mixed static and user inputs in the same position of a subcommand.");
                return Create(n => n.Parameter.Name == value, new StaticParameter(value.ToLower(), value, optional));
            }

            public CommandNode CreateUserInput(CommandParameter parameter)
            {
                if (Children.Any(n => n.Parameter.GetType() == typeof(StaticParameter)))
                    throw new InvalidOperationException("Cannot have mixed static and user inputs in the same position of a subcommand.");
                return Create(n => true, parameter);
            }

            private CommandNode Create(Predicate<CommandNode> predicate, CommandParameter parameter)
            {
                CommandNode? node = Children.FirstOrDefault(n => predicate(n));
                if (node == null)
                {
                    node = new CommandNode(parameter);
                    _children.Add(node);
                }
                if (node.Parameter.GetType() != parameter.GetType())
                    throw new Exception("User input argument already exist for this partial command.");
                return node;
            }

            public bool IsRequired()
            {
                return !Parameter.Optional;
            }
        }
    }
}