using System.Net.Http.Json;
using System.Text.Json;

namespace TwitchChatTTS.Helpers
{
    public class WebClientWrap
    {
        private HttpClient _client;
        private JsonSerializerOptions _options;


        public WebClientWrap(JsonSerializerOptions options)
        {
            _client = new HttpClient();
            _options = options;
        }


        public void AddHeader(string key, string? value)
        {
            if (_client.DefaultRequestHeaders.Contains(key))
                _client.DefaultRequestHeaders.Remove(key);
            _client.DefaultRequestHeaders.Add(key, value);
        }

        public async Task<T?> GetJson<T>(string uri)
        {
            var response = await _client.GetAsync(uri);
            return JsonSerializer.Deserialize<T>(await response.Content.ReadAsStreamAsync(), _options);
        }

        public async Task<HttpResponseMessage> Get(string uri)
        {
            return await _client.GetAsync(uri);
        }

        public async Task<HttpResponseMessage> Post<T>(string uri, T data)
        {
            return await _client.PostAsJsonAsync(uri, data);
        }

        public async Task<HttpResponseMessage> Post(string uri)
        {
            return await _client.PostAsJsonAsync(uri, new object());
        }
    }
}