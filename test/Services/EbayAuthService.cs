using Microsoft.Extensions.Configuration;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace test.Services
{
    public class EbayAuthService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _config;

        public EbayAuthService(HttpClient httpClient, IConfiguration config)
        {
            _httpClient = httpClient;
            _config = config;
        }

        public async Task<string?> GetAccessTokenAsync()
        {
            var clientId = _config["eBay:ClientId"];
            var clientSecret = _config["eBay:ClientSecret"];

            var credentials = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", credentials);

            // Client credentials = server-to-server flow
            var body = new StringContent(
                "grant_type=client_credentials&scope=https://api.ebay.com/oauth/api_scope",
                Encoding.UTF8,
                "application/x-www-form-urlencoded"
            );

            var response = await _httpClient.PostAsync("https://api.ebay.com/identity/v1/oauth2/token", body);

            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            return doc.RootElement.GetProperty("access_token").GetString();
        }
    }
}
