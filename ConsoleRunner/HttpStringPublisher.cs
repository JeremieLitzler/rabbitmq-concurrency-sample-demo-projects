using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using ConcurrencyTesting.Contracts;

namespace ConcurrencyTesting.ConsoleRunner
{
    /// <summary>An IStringPublisher that goes through the Web API instead of calling the client in-process, so ConcurrencyRunner drives it unchanged.</summary>
    public sealed class HttpStringPublisher : IStringPublisher
    {
        private readonly HttpClient _http;
        private readonly string _version;

        public HttpStringPublisher(string baseUrl, string version)
        {
            // net472 allows only 2 connections per host by default, which would serialize the load.
            System.Net.ServicePointManager.DefaultConnectionLimit = 1000;
            _http = new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(60) };
            _version = version; // the server is restarted before each run, so its singleton publisher is fresh
        }

        public Task ConnectAsync() => Post("connect", null);

        public Task PublishAsync(string message) => Post("publish", message);

        private async Task Post(string action, string message)
        {
            // The endpoint takes a JSON string body ([FromBody] string).
            var body = message == null
                ? new StringContent(string.Empty)
                : new StringContent("\"" + message + "\"", Encoding.UTF8, "application/json");
            HttpResponseMessage response = await _http.PostAsync("api/" + _version + "/" + action, body).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
                throw new HttpRequestException(Unquote(await response.Content.ReadAsStringAsync().ConfigureAwait(false)));
        }

        private static string Unquote(string s) => s.Length > 1 && s[0] == '"' ? s.Substring(1, s.Length - 2) : s;

        public void Dispose() => _http.Dispose();
    }
}
