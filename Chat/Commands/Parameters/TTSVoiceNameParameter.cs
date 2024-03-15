using Microsoft.Extensions.DependencyInjection;
namespace TwitchChatTTS.Chat.Commands.Parameters
{
    public class TTSVoiceNameParameter : ChatCommandParameter
    {
        private IServiceProvider _serviceProvider;

        public TTSVoiceNameParameter(IServiceProvider serviceProvider, bool optional = false) : base("TTS Voice Name", "Name of a TTS voice", optional)
        {
            _serviceProvider = serviceProvider;
        }

        public override bool Validate(string value)
        {
            var user = _serviceProvider.GetRequiredService<User>();
            if (user.VoicesAvailable == null)
                return false;

            value = value.ToLower();
            return user.VoicesAvailable.Any(e => e.Value.ToLower() == value);
        }
    }
}