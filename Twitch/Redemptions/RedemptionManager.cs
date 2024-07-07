using System.Reflection;
using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using Microsoft.Extensions.DependencyInjection;
using org.mariuszgromada.math.mxparser;
using Serilog;
using TwitchChatTTS.OBS.Socket.Data;
using TwitchChatTTS.OBS.Socket.Manager;

namespace TwitchChatTTS.Twitch.Redemptions
{
    public class RedemptionManager
    {
        private readonly IDictionary<string, IList<RedeemableAction>> _store;
        private readonly User _user;
        private readonly OBSManager _obsManager;
        private readonly SocketClient<WebSocketMessage> _hermesClient;
        private readonly ILogger _logger;
        private readonly Random _random;
        private bool _isReady;


        public RedemptionManager(
            User user,
            OBSManager obsManager,
            [FromKeyedServices("hermes")] SocketClient<WebSocketMessage> hermesClient,
            ILogger logger)
        {
            _store = new Dictionary<string, IList<RedeemableAction>>();
            _user = user;
            _obsManager = obsManager;
            _hermesClient = hermesClient;
            _logger = logger;
            _random = new Random();
            _isReady = false;
        }

        private void Add(string twitchRedemptionId, RedeemableAction action)
        {
            if (!_store.TryGetValue(twitchRedemptionId, out var actions))
                _store.Add(twitchRedemptionId, actions = new List<RedeemableAction>());

            actions.Add(action);
            _logger.Debug($"Added redemption action [name: {action.Name}][type: {action.Type}]");
        }

        public async Task Execute(RedeemableAction action, string senderDisplayName, long senderId)
        {
            try
            {
                switch (action.Type)
                {
                    case "WRITE_TO_FILE":
                        Directory.CreateDirectory(Path.GetDirectoryName(action.Data["file_path"]));
                        await File.WriteAllTextAsync(action.Data["file_path"], ReplaceContentText(action.Data["file_content"], senderDisplayName));
                        _logger.Debug($"Overwritten text to file [file: {action.Data["file_path"]}]");
                        break;
                    case "APPEND_TO_FILE":
                        Directory.CreateDirectory(Path.GetDirectoryName(action.Data["file_path"]));
                        await File.AppendAllTextAsync(action.Data["file_path"], ReplaceContentText(action.Data["file_content"], senderDisplayName));
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
                    case "TOGGLE_OBS_VISIBILITY":
                        await _obsManager.ToggleSceneItemVisibility(action.Data["scene_name"], action.Data["scene_item_name"]);
                        break;
                    case "SPECIFIC_OBS_VISIBILITY":
                        await _obsManager.UpdateSceneItemVisibility(action.Data["scene_name"], action.Data["scene_item_name"], action.Data["obs_visible"].ToLower() == "true");
                        break;
                    case "SPECIFIC_OBS_INDEX":
                        await _obsManager.UpdateSceneItemIndex(action.Data["scene_name"], action.Data["scene_item_name"], int.Parse(action.Data["obs_index"]));
                        break;
                    case "SLEEP":
                        _logger.Debug("Sleeping on thread due to redemption for OBS.");
                        await Task.Delay(int.Parse(action.Data["sleep"]));
                        break;
                    case "SPECIFIC_TTS_VOICE":
                        var voiceId = _user.VoicesAvailable.Keys.First(id => _user.VoicesAvailable[id].ToLower() == action.Data["tts_voice"].ToLower());
                        if (voiceId == null)
                        {
                            _logger.Warning($"Voice specified is not valid [voice: {action.Data["tts_voice"]}]");
                            return;
                        }
                        var voiceName = _user.VoicesAvailable[voiceId];
                        if (!_user.VoicesEnabled.Contains(voiceName))
                        {
                            _logger.Warning($"Voice specified is not enabled [voice: {action.Data["tts_voice"]}][voice id: {voiceId}]");
                            return;
                        }
                        await _hermesClient.Send(3, new HermesSocketLibrary.Socket.Data.RequestMessage()
                        {
                            Type = _user.VoicesSelected.ContainsKey(senderId) ? "update_tts_user" : "create_tts_user",
                            Data = new Dictionary<string, object>() { { "chatter", senderId }, { "voice", voiceId } }
                        });
                        _logger.Debug($"Changed the TTS voice of a chatter [voice: {action.Data["tts_voice"]}][display name: {senderDisplayName}][chatter id: {senderId}]");
                        break;
                    case "RANDOM_TTS_VOICE":
                        var voicesEnabled = _user.VoicesEnabled.ToList();
                        if (!voicesEnabled.Any())
                        {
                            _logger.Warning($"There are no TTS voices enabled [voice pool size: {voicesEnabled.Count}]");
                            return;
                        }
                        if (voicesEnabled.Count <= 1)
                        {
                            _logger.Warning($"There are not enough TTS voices enabled to randomize [voice pool size: {voicesEnabled.Count}]");
                            return;
                        }
                        var randomVoice = voicesEnabled[_random.Next(voicesEnabled.Count)];
                        var randomVoiceId = _user.VoicesAvailable.Keys.First(id => _user.VoicesAvailable[id] == randomVoice);
                        await _hermesClient.Send(3, new HermesSocketLibrary.Socket.Data.RequestMessage()
                        {
                            Type = _user.VoicesSelected.ContainsKey(senderId) ? "update_tts_user" : "create_tts_user",
                            Data = new Dictionary<string, object>() { { "chatter", senderId }, { "voice", randomVoiceId } }
                        });
                        _logger.Debug($"Randomly changed the TTS voice of a chatter [voice: {randomVoice}][display name: {senderDisplayName}][chatter id: {senderId}]");
                        break;
                    case "AUDIO_FILE":
                        if (!File.Exists(action.Data["file_path"]))
                        {
                            _logger.Warning($"Cannot find audio file for Twitch channel point redeem [file: {action.Data["file_path"]}]");
                            return;
                        }
                        AudioPlaybackEngine.Instance.PlaySound(action.Data["file_path"]);
                        _logger.Debug($"Played an audio file for channel point redeem [file: {action.Data["file_path"]}]");
                        break;
                    default:
                        _logger.Warning($"Unknown redeemable action has occured. Update needed? [type: {action.Type}]");
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

        public void Initialize(IEnumerable<Redemption> redemptions, IDictionary<string, RedeemableAction> actions)
        {
            _store.Clear();

            var ordered = redemptions.OrderBy(r => r.Order);
            foreach (var redemption in ordered)
            {
                try
                {
                    if (actions.TryGetValue(redemption.ActionName, out var action) && action != null)
                    {
                        _logger.Debug($"Fetched a redemption action [redemption id: {redemption.Id}][redemption action: {redemption.ActionName}][order: {redemption.Order}]");
                        Add(redemption.RedemptionId, action);
                    }
                    else
                        _logger.Warning($"Could not find redemption action [redemption id: {redemption.Id}][redemption action: {redemption.ActionName}][order: {redemption.Order}]");
                }
                catch (Exception e)
                {
                    _logger.Error(e, $"Failed to add a redemption [redemption id: {redemption.Id}][redemption action: {redemption.ActionName}][order: {redemption.Order}]");
                }
            }

            _isReady = true;
            _logger.Debug("All redemptions added. Redemption Manager is ready.");
        }

        private string ReplaceContentText(string content, string username)
        {
            return content.Replace("%USER%", username)
                .Replace("\\n", "\n");
        }
    }
}