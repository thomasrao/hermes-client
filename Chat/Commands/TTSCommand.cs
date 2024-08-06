using CommonSocketLibrary.Abstract;
using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TwitchChatTTS.Hermes.Socket;
using TwitchChatTTS.Twitch.Socket;
using TwitchChatTTS.Twitch.Socket.Messages;
using static TwitchChatTTS.Chat.Commands.TTSCommands;

namespace TwitchChatTTS.Chat.Commands
{
    public class TTSCommand : IChatCommand
    {
        private readonly TwitchWebsocketClient _twitch;
        private readonly User _user;
        private readonly TwitchApiClient _client;
        private readonly ILogger _logger;


        public TTSCommand(
            [FromKeyedServices("twitch")] SocketClient<TwitchWebsocketMessage> twitch,
            User user,
            TwitchApiClient client,
            ILogger logger)
        {
            _twitch = (twitch as TwitchWebsocketClient)!;
            _user = user;
            _client = client;
            _logger = logger;
        }

        public string Name => "tts";

        public void Build(ICommandBuilder builder)
        {
            builder.CreateCommandTree(Name, b =>
            {
                b.CreateStaticInputParameter("add", b =>
                {
                    b.CreateVoiceNameParameter("voiceName", false)
                        .CreateCommand(new AddTTSVoiceCommand(_user, _logger));
                })
                .AddAlias("insert", "add")
                .CreateStaticInputParameter("delete", b =>
                {
                    b.CreateVoiceNameParameter("voiceName", true)
                        .CreateCommand(new DeleteTTSVoiceCommand(_user, _logger));
                })
                .AddAlias("del", "delete")
                .AddAlias("remove", "delete")
                .CreateStaticInputParameter("enable", b =>
                {
                    b.CreateVoiceNameParameter("voiceName", false)
                        .CreateCommand(new SetTTSVoiceStateCommand(true, _user, _logger));
                })
                .AddAlias("on", "enable")
                .AddAlias("enabled", "enable")
                .AddAlias("true", "enable")
                .CreateStaticInputParameter("disable", b =>
                {
                    b.CreateVoiceNameParameter("voiceName", true)
                        .CreateCommand(new SetTTSVoiceStateCommand(false, _user, _logger));
                })
                .AddAlias("off", "disable")
                .AddAlias("disabled", "disable")
                .AddAlias("false", "disable")
                .CreateStaticInputParameter("join", b =>
                {
                    b.CreateMentionParameter("mention", true)
                        .AddPermission("tts.commands.tts.join")
                        .CreateCommand(new JoinRoomCommand(_twitch, _client, _user, _logger));
                })
                .CreateStaticInputParameter("leave", b =>
                {
                    b.CreateMentionParameter("mention", true)
                        .AddPermission("tts.commands.tts.leave")
                        .CreateCommand(new LeaveRoomCommand(_twitch, _client, _user, _logger));
                });
            });
        }

        private sealed class AddTTSVoiceCommand : IChatPartialCommand
        {
            private readonly User _user;
            private readonly ILogger _logger;

            public bool AcceptCustomPermission { get => false; }


            public AddTTSVoiceCommand(User user, ILogger logger)
            {
                _user = user;
                _logger = logger;
            }

            public async Task Execute(IDictionary<string, string> values, ChannelChatMessage message, HermesSocketClient client)
            {
                if (_user == null || _user.VoicesAvailable == null)
                    return;

                var voiceName = values["voiceName"];
                var voiceNameLower = voiceName.ToLower();
                var exists = _user.VoicesAvailable.Any(v => v.Value.ToLower() == voiceNameLower);
                if (exists)
                {
                    _logger.Warning($"Voice already exists [voice: {voiceName}][id: {message.ChatterUserId}]");
                    return;
                }

                await client.CreateTTSVoice(voiceName);
                _logger.Information($"Added a new TTS voice [voice: {voiceName}][creator: {message.ChatterUserLogin}][creator id: {message.ChatterUserId}]");
            }
        }

        private sealed class DeleteTTSVoiceCommand : IChatPartialCommand
        {
            private readonly User _user;
            private ILogger _logger;

            public bool AcceptCustomPermission { get => false; }

            public DeleteTTSVoiceCommand(User user, ILogger logger)
            {
                _user = user;
                _logger = logger;
            }

            public async Task Execute(IDictionary<string, string> values, ChannelChatMessage message, HermesSocketClient client)
            {
                if (_user == null || _user.VoicesAvailable == null)
                {
                    _logger.Warning($"Voices available are not loaded [chatter: {message.ChatterUserLogin}][chatter id: {message.ChatterUserId}]");
                    return;
                }

                var voiceName = values["voiceName"];
                var voiceNameLower = voiceName.ToLower();
                var exists = _user.VoicesAvailable.Any(v => v.Value.ToLower() == voiceNameLower);
                if (!exists)
                {
                    _logger.Warning($"Voice does not exist [voice: {voiceName}][chatter: {message.ChatterUserLogin}][chatter id: {message.ChatterUserId}]");
                    return;
                }

                var voiceId = _user.VoicesAvailable.FirstOrDefault(v => v.Value.ToLower() == voiceNameLower).Key;
                if (voiceId == null)
                {
                    _logger.Warning($"Could not find the identifier for the tts voice [voice name: {voiceName}]");
                    return;
                }

                await client.DeleteTTSVoice(voiceId);
                _logger.Information($"Deleted a TTS voice [voice: {voiceName}][chatter: {message.ChatterUserLogin}][chatter id: {message.ChatterUserId}]");
            }
        }

        private sealed class SetTTSVoiceStateCommand : IChatPartialCommand
        {
            private bool _state;
            private readonly User _user;
            private ILogger _logger;

            public bool AcceptCustomPermission { get => true; }

            public SetTTSVoiceStateCommand(bool state, User user, ILogger logger)
            {
                _state = state;
                _user = user;
                _logger = logger;
            }

            public async Task Execute(IDictionary<string, string> values, ChannelChatMessage message, HermesSocketClient client)
            {
                if (_user == null || _user.VoicesAvailable == null)
                    return;

                var voiceName = values["voiceName"];
                var voiceNameLower = voiceName.ToLower();
                var voiceId = _user.VoicesAvailable.FirstOrDefault(v => v.Value.ToLower() == voiceNameLower).Key;

                await client.UpdateTTSVoiceState(voiceId, _state);
                _logger.Information($"Changed state for TTS voice [voice: {voiceName}][state: {_state}][invoker: {message.ChatterUserLogin}][id: {message.ChatterUserId}]");
            }
        }

        private sealed class JoinRoomCommand : IChatPartialCommand
        {
            private readonly TwitchWebsocketClient _twitch;
            private readonly TwitchApiClient _client;
            private readonly User _user;
            private ILogger _logger;

            public bool AcceptCustomPermission { get => true; }

            public JoinRoomCommand(
                [FromKeyedServices("twitch")] SocketClient<TwitchWebsocketMessage> twitch,
                TwitchApiClient client,
                User user,
                ILogger logger
            )
            {
                _twitch = (twitch as TwitchWebsocketClient)!;
                _client = client;
                _user = user;
                _logger = logger;
            }

            public async Task Execute(IDictionary<string, string> values, ChannelChatMessage message, HermesSocketClient client)
            {
                var mention = values["mention"].ToLower();
                var fragment = message.Message.Fragments.FirstOrDefault(f => f.Mention != null && f.Text.ToLower() == mention);
                if (fragment == null)
                {
                    _logger.Warning("Cannot find the channel to join chat with.");
                    return;
                }

                string targetUserId = fragment.Mention!.UserId!;
                if (targetUserId == _user.TwitchUserId.ToString())
                {
                    _logger.Warning("Cannot join yourself.");
                    return;
                }

                string[] subscriptions = ["channel.chat.message", "channel.chat.message_delete", "channel.chat.clear_user_messages"];
                foreach (var subscription in subscriptions)
                {
                    _logger.Debug($"Attempting to subscribe to Twitch events [subscription: {subscription}]");
                    var data = await _client.CreateEventSubscription(subscription, "1", _twitch.SessionId, _user.TwitchUserId.ToString(), targetUserId);
                    var info = data?.Data?.FirstOrDefault();
                    if (info == null)
                    {
                        _logger.Warning("Could not find the subscription id.");
                        continue;
                    }
                    _twitch.AddSubscription(targetUserId, subscription, info.Id);
                }
                _logger.Information($"Joined chat room [channel: {fragment.Mention.UserLogin}][channel id: {targetUserId}][invoker: {message.ChatterUserLogin}][id: {message.ChatterUserId}]");
            }
        }

        private sealed class LeaveRoomCommand : IChatPartialCommand
        {
            private readonly TwitchWebsocketClient _twitch;
            private readonly TwitchApiClient _client;
            private readonly User _user;
            private ILogger _logger;

            public bool AcceptCustomPermission { get => true; }

            public LeaveRoomCommand(
                [FromKeyedServices("twitch")] SocketClient<TwitchWebsocketMessage> twitch,
                TwitchApiClient client,
                User user,
                ILogger logger
            )
            {
                _twitch = (twitch as TwitchWebsocketClient)!;
                _client = client;
                _user = user;
                _logger = logger;
            }

            public async Task Execute(IDictionary<string, string> values, ChannelChatMessage message, HermesSocketClient client)
            {
                var mention = values["mention"].ToLower();
                var fragment = message.Message.Fragments.FirstOrDefault(f => f.Mention != null && f.Text.ToLower() == mention);
                if (fragment?.Mention == null)
                {
                    _logger.Warning("Cannot find the channel to leave chat from.");
                    return;
                }

                string targetUserId = fragment.Mention!.UserId!;
                if (targetUserId == _user.TwitchUserId.ToString())
                {
                    _logger.Warning("Cannot join yourself.");
                    return;
                }

                string[] subscriptions = ["channel.chat.message", "channel.chat.message_delete", "channel.chat.clear_user_messages"];
                foreach (var subscription in subscriptions)
                {
                    var subscriptionId = _twitch.GetSubscriptionId(targetUserId, subscription);
                    if (subscriptionId == null)
                    {
                        _logger.Warning($"Cannot find the subscription for that channel  [subscription: {subscription}]");
                        continue;
                    }

                    try
                    {
                        await _client.DeleteEventSubscription(subscriptionId);
                        _twitch.RemoveSubscription(targetUserId, subscription);
                        _logger.Information($"Left chat room [channel: {fragment.Mention.UserLogin}][channel id: {targetUserId}][invoker: {message.ChatterUserLogin}][id: {message.ChatterUserId}]");
                    }
                    catch (Exception ex)
                    {
                        _logger.Error(ex, $"Failed to delete the subscription from Twitch [subscription: {subscription}][subscription id: {subscriptionId}]");
                    }
                }
            }
        }
    }
}