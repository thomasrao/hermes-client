using Serilog;
using TwitchLib.Client.Models;

namespace TwitchChatTTS.Chat.Commands
{
    public class RefreshTTSDataCommand : ChatCommand
    {
        private readonly User _user;
        private readonly HermesApiClient _hermesApi;
        private readonly ILogger _logger;

        public RefreshTTSDataCommand(User user, HermesApiClient hermesApi, ILogger logger)
        : base("refresh", "Refreshes certain TTS related data on the client.")
        {
            _user = user;
            _hermesApi = hermesApi;
            _logger = logger;
        }

        public override async Task<bool> CheckPermissions(ChatMessage message, long broadcasterId)
        {
            return message.IsModerator || message.IsBroadcaster;
        }

        public override async Task Execute(IList<string> args, ChatMessage message, long broadcasterId)
        {
            var service = args.FirstOrDefault();
            if (service == null)
                return;

            switch (service)
            {
                case "tts_voice_enabled":
                    var voicesEnabled = await _hermesApi.FetchTTSEnabledVoices();
                    if (voicesEnabled == null || !voicesEnabled.Any())
                        _user.VoicesEnabled = new HashSet<string>(["Brian"]);
                    else
                        _user.VoicesEnabled = new HashSet<string>(voicesEnabled.Select(v => v));
                    _logger.Information($"{_user.VoicesEnabled.Count} TTS voices have been enabled.");
                    break;
                case "word_filters":
                    var wordFilters = await _hermesApi.FetchTTSWordFilters();
                    _user.RegexFilters = wordFilters.ToList();
                    _logger.Information($"{_user.RegexFilters.Count()} TTS word filters.");
                    break;
                case "username_filters":
                    var usernameFilters = await _hermesApi.FetchTTSUsernameFilters();
                    _user.ChatterFilters = usernameFilters.ToDictionary(e => e.Username, e => e);
                    _logger.Information($"{_user.ChatterFilters.Where(f => f.Value.Tag == "blacklisted").Count()} username(s) have been blocked.");
                    _logger.Information($"{_user.ChatterFilters.Where(f => f.Value.Tag == "priority").Count()} user(s) have been prioritized.");
                    break;
                case "default_voice":
                    _user.DefaultTTSVoice = await _hermesApi.FetchTTSDefaultVoice();
                    _logger.Information("Default Voice: " + _user.DefaultTTSVoice);
                    break;
            }
        }
    }
}