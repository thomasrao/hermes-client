using System.Reflection;
using CommonSocketLibrary.Abstract;
using CommonSocketLibrary.Common;
using HermesSocketLibrary.Requests.Messages;
using Microsoft.Extensions.DependencyInjection;
using org.mariuszgromada.math.mxparser;
using Serilog;
using TwitchChatTTS.Hermes.Socket;
using TwitchChatTTS.OBS.Socket;
using TwitchChatTTS.OBS.Socket.Data;

namespace TwitchChatTTS.Twitch.Redemptions
{
    public class RedemptionManager
    {
        private readonly IDictionary<string, IList<RedeemableAction>> _store;
        private readonly User _user;
        private readonly OBSSocketClient _obs;
        private readonly HermesSocketClient _hermes;
        private readonly ILogger _logger;
        private readonly Random _random;
        private bool _isReady;


        public RedemptionManager(
            User user,
            [FromKeyedServices("obs")] SocketClient<WebSocketMessage> obs,
            [FromKeyedServices("hermes")] SocketClient<WebSocketMessage> hermes,
            ILogger logger)
        {
            _store = new Dictionary<string, IList<RedeemableAction>>();
            _user = user;
            _obs = (obs as OBSSocketClient)!;
            _hermes = (hermes as HermesSocketClient)!;
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
            _logger.Debug($"Executing an action for a redemption [action: {action.Name}][action type: {action.Type}][chatter: {senderDisplayName}][chatter id: {senderId}]");

            if (action.Data == null)
            {
                _logger.Warning($"No data was provided for an action, caused by redemption [action: {action.Name}][action type: {action.Type}][chatter: {senderDisplayName}][chatter id: {senderId}]");
                return;
            }

            try
            {
                switch (action.Type)
                {
                    case "WRITE_TO_FILE":
                        Directory.CreateDirectory(Path.GetDirectoryName(action.Data["file_path"]));
                        await File.WriteAllTextAsync(action.Data["file_path"], ReplaceContentText(action.Data["file_content"], senderDisplayName));
                        _logger.Debug($"Overwritten text to file [file: {action.Data["file_path"]}][chatter: {senderDisplayName}][chatter id: {senderId}]");
                        break;
                    case "APPEND_TO_FILE":
                        Directory.CreateDirectory(Path.GetDirectoryName(action.Data["file_path"]));
                        await File.AppendAllTextAsync(action.Data["file_path"], ReplaceContentText(action.Data["file_content"], senderDisplayName));
                        _logger.Debug($"Appended text to file [file: {action.Data["file_path"]}][chatter: {senderDisplayName}][chatter id: {senderId}]");
                        break;
                    case "OBS_TRANSFORM":
                        var type = typeof(OBSTransformationData);
                        await _obs.UpdateTransformation(action.Data["scene_name"], action.Data["scene_item_name"], (d) =>
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
                                    _logger.Warning($"Failed to find property for OBS transformation [scene: {action.Data["scene_name"]}][source: {action.Data["scene_item_name"]}][property: {propertyName}][chatter: {senderDisplayName}][chatter id: {senderId}]");
                                    continue;
                                }

                                var currentValue = prop.GetValue(d);
                                if (currentValue == null)
                                {
                                    _logger.Warning($"Found a null value from OBS transformation [scene: {action.Data["scene_name"]}][source: {action.Data["scene_item_name"]}][property: {propertyName}][chatter: {senderDisplayName}][chatter id: {senderId}]");
                                    continue;
                                }

                                Expression expression = new Expression(expressionString);
                                expression.addConstants(new Constant("x", (double?)currentValue ?? 0.0d));
                                if (!expression.checkSyntax())
                                {
                                    _logger.Warning($"Could not parse math expression for OBS transformation [scene: {action.Data["scene_name"]}][source: {action.Data["scene_item_name"]}][expression: {expressionString}][property: {propertyName}][chatter: {senderDisplayName}][chatter id: {senderId}]");
                                    continue;
                                }

                                var newValue = expression.calculate();
                                prop.SetValue(d, newValue);
                                _logger.Debug($"OBS transformation [scene: {action.Data["scene_name"]}][source: {action.Data["scene_item_name"]}][property: {propertyName}][old value: {currentValue}][new value: {newValue}][expression: {expressionString}][chatter: {senderDisplayName}][chatter id: {senderId}]");
                            }
                            _logger.Debug($"Finished applying the OBS transformation property changes [scene: {action.Data["scene_name"]}][source: {action.Data["scene_item_name"]}][chatter: {senderDisplayName}][chatter id: {senderId}]");
                        });
                        break;
                    case "TOGGLE_OBS_VISIBILITY":
                        await _obs.ToggleSceneItemVisibility(action.Data["scene_name"], action.Data["scene_item_name"]);
                        break;
                    case "SPECIFIC_OBS_VISIBILITY":
                        await _obs.UpdateSceneItemVisibility(action.Data["scene_name"], action.Data["scene_item_name"], action.Data["obs_visible"].ToLower() == "true");
                        break;
                    case "SPECIFIC_OBS_INDEX":
                        await _obs.UpdateSceneItemIndex(action.Data["scene_name"], action.Data["scene_item_name"], int.Parse(action.Data["obs_index"]));
                        break;
                    case "SLEEP":
                        _logger.Debug("Sleeping on thread due to redemption for OBS.");
                        await Task.Delay(int.Parse(action.Data["sleep"]));
                        break;
                    case "SPECIFIC_TTS_VOICE":
                    case "RANDOM_TTS_VOICE":
                        string voiceId = string.Empty;
                        bool specific = action.Type == "SPECIFIC_TTS_VOICE";

                        var voicesEnabled = _user.VoicesEnabled.ToList();
                        if (specific)
                            voiceId = _user.VoicesAvailable.Keys.First(id => _user.VoicesAvailable[id].ToLower() == action.Data["tts_voice"].ToLower());
                        else
                        {
                            if (!voicesEnabled.Any())
                            {
                                _logger.Warning($"There are no TTS voices enabled [voice pool size: {voicesEnabled.Count}][chatter: {senderDisplayName}][chatter id: {senderId}]");
                                return;
                            }
                            if (voicesEnabled.Count <= 1)
                            {
                                _logger.Warning($"There are not enough TTS voices enabled to randomize [voice pool size: {voicesEnabled.Count}][chatter: {senderDisplayName}][chatter id: {senderId}]");
                                return;
                            }

                            string? selectedId = null;
                            if (!_user.VoicesSelected.ContainsKey(senderId))
                                selectedId = _user.VoicesAvailable.Keys.First(id => _user.VoicesAvailable[id] == _user.DefaultTTSVoice);
                            else
                                selectedId = _user.VoicesSelected[senderId];

                            do
                            {
                                var randomVoice = voicesEnabled[_random.Next(voicesEnabled.Count)];
                                voiceId = _user.VoicesAvailable.Keys.First(id => _user.VoicesAvailable[id] == randomVoice);
                            } while (voiceId == selectedId);
                        }
                        if (string.IsNullOrEmpty(voiceId))
                        {
                            _logger.Warning($"Voice is not valid [voice: {action.Data["tts_voice"]}][voice pool size: {voicesEnabled.Count}][source: redemption][chatter: {senderDisplayName}][chatter id: {senderId}]");
                            return;
                        }
                        var voiceName = _user.VoicesAvailable[voiceId];
                        if (!_user.VoicesEnabled.Contains(voiceName))
                        {
                            _logger.Warning($"Voice is not enabled [voice: {action.Data["tts_voice"]}][voice pool size: {voicesEnabled.Count}][voice id: {voiceId}][source: redemption][chatter: {senderDisplayName}][chatter id: {senderId}]");
                            return;
                        }

                        if (_user.VoicesSelected.ContainsKey(senderId))
                        {
                            await _hermes.UpdateTTSUser(senderId, voiceId);
                            _logger.Debug($"Sent request to create chat TTS voice [voice: {voiceName}][chatter id: {senderId}][source: redemption][chatter: {senderDisplayName}][chatter id: {senderId}]");
                        }
                        else
                        {
                            await _hermes.CreateTTSUser(senderId, voiceId);
                            _logger.Debug($"Sent request to update chat TTS voice [voice: {voiceName}][chatter id: {senderId}][source: redemption][chatter: {senderDisplayName}][chatter id: {senderId}]");
                        }
                        break;
                    case "AUDIO_FILE":
                        if (!File.Exists(action.Data["file_path"]))
                        {
                            _logger.Warning($"Cannot find audio file for Twitch channel point redeem [file: {action.Data["file_path"]}][chatter: {senderDisplayName}][chatter id: {senderId}]");
                            return;
                        }
                        AudioPlaybackEngine.Instance.PlaySound(action.Data["file_path"]);
                        _logger.Debug($"Played an audio file for channel point redeem [file: {action.Data["file_path"]}][chatter: {senderDisplayName}][chatter id: {senderId}]");
                        break;
                    default:
                        _logger.Warning($"Unknown redeemable action has occured. Update needed? [type: {action.Type}][chatter: {senderDisplayName}][chatter id: {senderId}]");
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, $"Failed to execute a redemption action [action: {action.Name}][action type: {action.Type}][chatter: {senderDisplayName}][chatter id: {senderId}]");
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

            var ordered = redemptions.Where(r => r != null).OrderBy(r => r.Order);
            foreach (var redemption in ordered)
            {
                if (redemption.ActionName == null)
                {
                    _logger.Warning("Null value found for the action name of a redemption.");
                    continue;
                }

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