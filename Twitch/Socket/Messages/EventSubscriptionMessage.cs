using System.Text.Json.Serialization;

namespace TwitchChatTTS.Twitch.Socket.Messages
{
    public class EventSubscriptionMessage : IVersionedMessage
    {
        public string Type { get; set; }
        public string Version { get; set; }
        public IDictionary<string, string> Condition { get; set; }
        public EventSubTransport Transport { get; set; }
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public int? Cost { get; set; }

        public EventSubscriptionMessage()
        {
            Type = string.Empty;
            Version = string.Empty;
            Condition = new Dictionary<string, string>();
            Transport = new EventSubTransport();
        }

        public EventSubscriptionMessage(string type, string version, string callback, string secret, IDictionary<string, string>? conditions = null)
        {
            Type = type;
            Version = version;
            Condition = conditions ?? new Dictionary<string, string>();
            Transport = new EventSubTransport("webhook", callback, secret);
        }

        public EventSubscriptionMessage(string type, string version, string sessionId, IDictionary<string, string>? conditions = null)
        {
            Type = type;
            Version = version;
            Condition = conditions ?? new Dictionary<string, string>();
            Transport = new EventSubTransport("websocket", sessionId);
        }


        public class EventSubTransport
        {
            public string Method { get; }
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string? Callback { get; }
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string? Secret { get; }
            [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
            public string? SessionId { get; }

            public EventSubTransport()
            {
                Method = string.Empty;
            }

            public EventSubTransport(string method, string callback, string secret)
            {
                Method = method;
                Callback = callback;
                Secret = secret;
            }

            public EventSubTransport(string method, string sessionId)
            {
                Method = method;
                SessionId = sessionId;
            }
        }
    }
}