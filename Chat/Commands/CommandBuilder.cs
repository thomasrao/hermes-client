using Serilog;
using TwitchChatTTS.Chat.Commands.Parameters;

namespace TwitchChatTTS.Chat.Commands
{
    public static class TTSCommands
    {
        public interface ICommandBuilder
        {
            ICommandSelector Build();
            void Clear();
            ICommandBuilder CreateCommandTree(string name, Action<ICommandBuilder> callback);
            ICommandBuilder CreateCommand(IChatPartialCommand command);
            ICommandBuilder CreateStaticInputParameter(string value, Action<ICommandBuilder> callback, bool optional = false);
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
                Clear();
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
            CommandSelectorResult GetBestMatch(string[] args);
            IDictionary<string, CommandParameter> GetNonStaticArguments(string[] args, string path);
            CommandValidationResult Validate(string[] args, string path);
        }

        public sealed class CommandSelector : ICommandSelector
        {
            private CommandNode _root;

            public CommandSelector(CommandNode root)
            {
                _root = root;
            }

            public CommandSelectorResult GetBestMatch(string[] args)
            {
                return GetBestMatch(_root, args, null, string.Empty);
            }

            private CommandSelectorResult GetBestMatch(CommandNode node, IEnumerable<string> args, IChatPartialCommand? match, string path)
            {
                if (node == null || !args.Any())
                    return new CommandSelectorResult(match, path);
                if (!node.Children.Any())
                    return new CommandSelectorResult(node.Command ?? match, path);

                var argument = args.First();
                var argumentLower = argument.ToLower();
                foreach (var child in node.Children)
                {
                    if (child.Parameter.GetType() == typeof(StaticParameter))
                    {
                        if (child.Parameter.Name.ToLower() == argumentLower)
                        {
                            return GetBestMatch(child, args.Skip(1), child.Command ?? match, (path.Length == 0 ? string.Empty : path + ".") + child.Parameter.Name.ToLower());
                        }
                        continue;
                    }

                    return GetBestMatch(child, args.Skip(1), child.Command ?? match, (path.Length == 0 ? string.Empty : path + ".") + "*");
                }

                return new CommandSelectorResult(match, path);
            }

            public CommandValidationResult Validate(string[] args, string path)
            {
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

                        if (!current.Parameter.Validate(args[i]))
                        {
                            return new CommandValidationResult(false, args[i]);
                        }
                    }
                    else
                    {
                        current = current.Children.FirstOrDefault(n => n.Parameter.GetType() == typeof(StaticParameter) && n.Parameter.Name == part);
                        if (current == null)
                            throw new Exception($"Cannot find command path [path: {path}][subpath: {part}]");
                    }
                }

                return new CommandValidationResult(true, null);
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

            public CommandSelectorResult(IChatPartialCommand? command, string path)
            {
                Command = command;
                Path = path;
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
            public IList<CommandNode> Children { get => _children.AsReadOnly(); }

            private IList<CommandNode> _children;

            public CommandNode(CommandParameter parameter)
            {
                Parameter = parameter;
                _children = new List<CommandNode>();
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