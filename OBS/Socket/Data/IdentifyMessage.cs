using System.Text.Json.Serialization;

namespace TwitchChatTTS.OBS.Socket.Data
{
    public class IdentifyMessage
    {
        public int RpcVersion { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string? Authentication { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? EventSubscriptions { get; set; }

        public IdentifyMessage(int rpcVersion, string? authentication, int? subscriptions)
        {
            RpcVersion = rpcVersion;
            Authentication = authentication;
            EventSubscriptions = subscriptions;
        }
    }
}