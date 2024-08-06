using System.Net.Http.Json;
using System.Text.Json;

namespace TwitchChatTTS.Helpers
{
    public class WebClientWrap
    {
        private readonly HttpClient _client;
        private readonly JsonSerializerOptions _options;


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

        public async Task<T?> GetJson<T>(string uri, JsonSerializerOptions? options = null)
        {
            var response = await _client.GetAsync(uri);
            return JsonSerializer.Deserialize<T>(await response.Content.ReadAsStreamAsync(), options ?? _options);
        }

        public async Task<HttpResponseMessage> Get(string uri)
        {
            return await _client.GetAsync(uri);
        }

        public async Task<HttpResponseMessage> Post<T>(string uri, T data)
        {
            return await _client.PostAsJsonAsync(uri, data, _options);
        }

        public async Task<HttpResponseMessage> Post(string uri)
        {
            return await _client.PostAsJsonAsync(uri, new object(), _options);
        }

        public async Task<T?> Delete<T>(string uri)
        {
            return await _client.DeleteFromJsonAsync<T>(uri, _options);
        }

        public async Task<HttpResponseMessage> Delete(string uri)
        {
            return await _client.DeleteAsync(uri);
        }
    }
}