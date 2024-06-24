using System.Reflection;
using org.mariuszgromada.math.mxparser;
using Serilog;
using TwitchChatTTS.OBS.Socket.Data;
using TwitchChatTTS.OBS.Socket.Manager;

namespace TwitchChatTTS.Twitch.Redemptions
{
    public class RedemptionManager
    {
        private readonly IList<Redemption> _redemptions;
        private readonly IDictionary<string, RedeemableAction> _actions;
        private readonly IDictionary<string, IList<RedeemableAction>> _store;
        private readonly OBSManager _obsManager;
        private readonly ILogger _logger;
        private bool _isReady;


        public RedemptionManager(OBSManager obsManager, ILogger logger)
        {
            _redemptions = new List<Redemption>();
            _actions = new Dictionary<string, RedeemableAction>();
            _store = new Dictionary<string, IList<RedeemableAction>>();
            _obsManager = obsManager;
            _logger = logger;
            _isReady = false;
        }

        public void AddTwitchRedemption(Redemption redemption)
        {
            _redemptions.Add(redemption);
        }

        public void AddAction(RedeemableAction action)
        {
            _actions.Add(action.Name, action);
        }

        private void Add(string twitchRedemptionId, RedeemableAction action)
        {
            if (!_store.TryGetValue(twitchRedemptionId, out var actions))
                _store.Add(twitchRedemptionId, actions = new List<RedeemableAction>());
            actions.Add(action);
            _store[twitchRedemptionId] = actions.OrderBy(a => a).ToList();
        }

        public async Task Execute(RedeemableAction action, string sender)
        {
            try
            {
                switch (action.Type)
                {
                    case "WRITE_TO_FILE":
                        Directory.CreateDirectory(Path.GetDirectoryName(action.Data["file_path"]));
                        await File.WriteAllTextAsync(action.Data["file_path"], ReplaceContentText(action.Data["file_content"], sender));
                        _logger.Debug($"Overwritten text to file [file: {action.Data["file_path"]}]");
                        break;
                    case "APPEND_TO_FILE":
                        Directory.CreateDirectory(Path.GetDirectoryName(action.Data["file_path"]));
                        await File.AppendAllTextAsync(action.Data["file_path"], ReplaceContentText(action.Data["file_content"], sender));
                        _logger.Debug($"Appended text to file [file: {action.Data["file_path"]}]");
                        break;
                    case "OBS_TRANSFORM":
                        var type = typeof(OBSTransformationData);
                        await _obsManager.UpdateTransformation(action.Data["scene_name"], action.Data["scene_item_name"], (d) =>
                        {
                            string[] properties = ["rotation", "position_x", "position_y"];
                            foreach (var property in properties)
                            {
                                if (!action.Data.TryGetValue(property, out var expressionString) || expressionString == null)
                                    continue;

                                var propertyName = string.Join("", property.Split('_').Select(p => char.ToUpper(p[0]) + p.Substring(1)));
                                PropertyInfo? prop = type.GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
                                if (prop == null)
                                {
                                    _logger.Warning($"Failed to find property for OBS transformation [scene: {action.Data["scene_name"]}][source: {action.Data["scene_item_name"]}][property: {propertyName}]");
                                    continue;
                                }

                                var currentValue = prop.GetValue(d);
                                if (currentValue == null)
                                {
                                    _logger.Warning($"Found a null value from OBS transformation [scene: {action.Data["scene_name"]}][source: {action.Data["scene_item_name"]}][property: {propertyName}]");
                                }

                                Expression expression = new Expression(expressionString);
                                expression.addConstants(new Constant("x", (double?)currentValue ?? 0.0d));
                                if (!expression.checkSyntax())
                                {
                                    _logger.Warning($"Could not parse math expression for OBS transformation [scene: {action.Data["scene_name"]}][source: {action.Data["scene_item_name"]}][expression: {expressionString}][property: {propertyName}]");
                                    continue;
                                }

                                var newValue = expression.calculate();
                                prop.SetValue(d, newValue);
                                _logger.Debug($"OBS transformation [scene: {action.Data["scene_name"]}][source: {action.Data["scene_item_name"]}][property: {propertyName}][old value: {currentValue}][new value: {newValue}][expression: {expressionString}]");
                            }
                            _logger.Debug($"Finished applying the OBS transformation property changes [scene: {action.Data["scene_name"]}][source: {action.Data["scene_item_name"]}]");
                        });
                        break;
                    case "AUDIO_FILE":
                        if (!File.Exists(action.Data["file_path"])) {
                            _logger.Warning($"Cannot find audio file for Twitch channel point redeem [file: {action.Data["file_path"]}]");
                            return;
                        }
                        AudioPlaybackEngine.Instance.PlaySound(action.Data["file_path"]);
                        _logger.Debug($"Played an audio file for channel point redeem [file: {action.Data["file_path"]}]");
                        break;
                    default:
                        _logger.Warning($"Unknown redeemable action has occured [type: {action.Type}]");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Failed to execute a redemption action.");
            }
        }

        public IList<RedeemableAction> Get(string twitchRedemptionId)
        {
            if (!_isReady)
                throw new InvalidOperationException("Not ready");

            if (_store.TryGetValue(twitchRedemptionId, out var actions))
                return actions;
            return new List<RedeemableAction>(0);
        }

        public void Ready()
        {
            var ordered = _redemptions.OrderBy(r => r.Order);
            _store.Clear();

            foreach (var redemption in ordered)
                if (_actions.TryGetValue(redemption.ActionName, out var action) && action != null)
                    Add(redemption.RedemptionId, action);
            
            _isReady = true;
            _logger.Debug("Redemption Manager is ready.");
        }

        private string ReplaceContentText(string content, string username) {
            return content.Replace("%USER%", username);
        }
    }
}