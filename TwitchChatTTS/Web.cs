

using System.Net;
using System.Net.Http.Json;

public static class WebHelper {
    private static HttpClient _client = new HttpClient();

    public static void AddHeader(string key, string? value) {
        _client.DefaultRequestHeaders.Add(key, value);
        //ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls13;
    }

    public static async Task<T?> GetJson<T>(string uri) {
        return (T) await _client.GetFromJsonAsync(uri, typeof(T));
    }

    public static async Task<HttpResponseMessage> Get(string uri) {
        return await _client.GetAsync(uri);
    }

    public static async Task<HttpResponseMessage> Post<T>(string uri, T data) {
        return await _client.PostAsJsonAsync(uri, data);
    }

    public static async Task<HttpResponseMessage> Post(string uri) {
        return await _client.PostAsJsonAsync(uri, new object());
    }
}