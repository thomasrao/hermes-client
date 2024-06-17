using Microsoft.Extensions.DependencyInjection;
using Serilog;
using TwitchLib.Client.Models;

namespace TwitchChatTTS.Chat.Commands
{
    public class RefreshTTSDataCommand : ChatCommand
    {
        private IServiceProvider _serviceProvider;
        private ILogger _logger;

        public RefreshTTSDataCommand(IServiceProvider serviceProvider, ILogger logger)
        : base("refresh", "Refreshes certain TTS related data on the client.")
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        public override async Task<bool> CheckPermissions(ChatMessage message, long broadcasterId)
        {
            return message.IsModerator || message.IsBroadcaster;
        }

        public override async Task Execute(IList<string> args, ChatMessage message, long broadcasterId)
        {
            var user = _serviceProvider.GetRequiredService<User>();
            var service = args.FirstOrDefault();
            if (service == null)
                return;

            var hermes = _serviceProvider.GetRequiredService<HermesApiClient>();

            switch (service)
            {
                case "tts_voice_enabled":
                    var voicesEnabled = await hermes.FetchTTSEnabledVoices();
                    if (voicesEnabled == null || !voicesEnabled.Any())
                        user.VoicesEnabled = new HashSet<string>(new string[] { "Brian" });
                    else
                        user.VoicesEnabled = new HashSet<string>(voicesEnabled.Select(v => v));
                    _logger.Information($"{user.VoicesEnabled.Count} TTS voices have been enabled.");
                    break;
                case "word_filters":
                    var wordFilters = await hermes.FetchTTSWordFilters();
                    user.RegexFilters = wordFilters.ToList();
                    _logger.Information($"{user.RegexFilters.Count()} TTS word filters.");
                    break;
                case "username_filters":
                    var usernameFilters = await hermes.FetchTTSUsernameFilters();
                    user.ChatterFilters = usernameFilters.ToDictionary(e => e.Username, e => e);
                    _logger.Information($"{user.ChatterFilters.Where(f => f.Value.Tag == "blacklisted").Count()} username(s) have been blocked.");
                    _logger.Information($"{user.ChatterFilters.Where(f => f.Value.Tag == "priority").Count()} user(s) have been prioritized.");
                    break;
                case "default_voice":
                    user.DefaultTTSVoice = await hermes.FetchTTSDefaultVoice();
                    _logger.Information("Default Voice: " + user.DefaultTTSVoice);
                    break;
            }
        }
    }
}