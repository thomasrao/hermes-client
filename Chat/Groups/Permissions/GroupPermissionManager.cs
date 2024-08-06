using System.Collections.ObjectModel;
using Serilog;

namespace TwitchChatTTS.Chat.Groups.Permissions
{
    public class GroupPermissionManager : IGroupPermissionManager
    {
        private PermissionNode _root;
        private ILogger _logger;


        public GroupPermissionManager(ILogger logger)
        {
            _logger = logger;
            _root = new PermissionNode(string.Empty, null, null);
        }


        public bool? CheckIfAllowed(string path)
        {
            var res = Get(path)!.Allow;
            _logger.Debug($"Permission Node GET {path} = {res?.ToString() ?? "null"}");
            return res;
        }

        public bool? CheckIfDirectAllowed(string path)
        {
            var node = Get(path, nullIfMissing: true);
            if (node == null)
                return null;
            
            var res = node.DirectAllow;
            _logger.Debug($"Permission Node GET {path} = {res?.ToString() ?? "null"} [direct]");
            return res;
        }

        public bool? CheckIfAllowed(IEnumerable<string> groups, string path)
        {
            bool overall = true;
            foreach (var group in groups)
            {
                var result = CheckIfAllowed($"{group}.{path}");
                if (result == true)
                    return true;
                if (result == false)
                    overall = false;
            }
            return overall ? null : false;
        }

        public bool? CheckIfDirectAllowed(IEnumerable<string> groups, string path)
        {
            bool overall = false;
            foreach (var group in groups)
            {
                var result = CheckIfDirectAllowed($"{group}.{path}");
                if (result == false)
                    return false;
                if (result == true)
                    overall = true;
            }
            return overall ? true : null;
        }

        public void Clear()
        {
            _root.Clear();
        }

        public bool Remove(string path)
        {
            var node = Get(path);
            if (node == null || node.Parent == null)
                return false;

            var parts = path.Split('.');
            var last = parts.Last();
            if (parts.Length > 1 && parts[parts.Length - 1] == node.Parent.Name || parts.Length == 1 && node.Parent.Name == null)
            {
                node.Parent.Remove(last);
                _logger.Debug($"Permission Node REMOVE priv {path}");
                return true;
            }
            return false;
        }

        public void Set(string path, bool? allow)
        {
            var node = Get(path, true);
            node!.Allow = allow;
            _logger.Debug($"Permission Node ADD {path} = {allow?.ToString() ?? "null"}");
        }

        private PermissionNode? Get(string path, bool edit = false, bool nullIfMissing = false)
        {
            return Get(_root, path.ToLower(), edit, nullIfMissing);
        }

        private PermissionNode? Get(PermissionNode node, string path, bool edit, bool nullIfMissing)
        {
            if (path.Length == 0)
                return node;

            var parts = path.Split('.');
            var name = parts.First();
            var next = node.Children?.FirstOrDefault(n => n.Name == name);
            if (next == null)
            {
                if (!edit)
                    return nullIfMissing ? null : node;

                next = new PermissionNode(name, node, null);
                node.Add(next);
            }
            return Get(next, string.Join('.', parts.Skip(1)), edit, nullIfMissing);
        }

        private sealed class PermissionNode
        {
            public string Name { get; }
            public bool? Allow
            {
                get
                {
                    var current = this;
                    while (current._allow == null && current._parent != null)
                        current = current._parent;
                    return current._allow;
                }
                set => _allow = value;
            }
            public bool? DirectAllow { get => _allow; }

            internal PermissionNode? Parent { get => _parent; }
            public IList<PermissionNode>? Children { get => _children == null ? null : new ReadOnlyCollection<PermissionNode>(_children); }

            private bool? _allow;
            private PermissionNode? _parent;
            private IList<PermissionNode>? _children;


            public PermissionNode(string name, PermissionNode? parent, bool? allow)
            {
                Name = name;
                _parent = parent;
                _allow = allow;
            }

            internal void Add(PermissionNode child)
            {
                if (_children == null)
                    _children = new List<PermissionNode>();
                _children.Add(child);
            }

            internal void Clear()
            {
                if (_children != null)
                    _children.Clear();
            }

            public void Remove(string name)
            {
                if (_children == null || !_children.Any())
                    return;

                for (var i = 0; i < _children.Count; i++)
                {
                    if (_children[i].Name == name)
                    {
                        _children.RemoveAt(i);
                        break;
                    }
                }
            }
        }
    }
}