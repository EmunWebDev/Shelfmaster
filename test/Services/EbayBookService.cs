using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;

namespace test.Services
{
    public class EbayBookService
    {
        private readonly HttpClient _httpClient;

        public EbayBookService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        // Fetch price in USD from eBay
        public async Task<decimal?> GetBookPriceUsdAsync(string isbn, string accessToken)
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", accessToken);

            var url = $"https://api.ebay.com/buy/browse/v1/item_summary/search?q={isbn}&category_ids=267";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (!doc.RootElement.TryGetProperty("itemSummaries", out var items) || items.GetArrayLength() == 0)
                return null;

            var priceValue = items[0].GetProperty("price").GetProperty("value").GetString();

            if (decimal.TryParse(priceValue, out decimal usdPrice))
            {
                return usdPrice;
            }

            return null;
        }

        // Fetch conversion rate USD → PHP
        public async Task<decimal> GetUsdToPhpRateAsync()
        {
            var url = "https://api.exchangerate.host/latest?base=USD&symbols=PHP";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return 58m; // fallback

            var json = await response.Content.ReadAsStringAsync();
            using var doc = JsonDocument.Parse(json);

            if (doc.RootElement.TryGetProperty("rates", out var rates)
                && rates.TryGetProperty("PHP", out var phpRate))
            {
                return phpRate.GetDecimal();
            }

            return 58m; // fallback rate if API fails
        }

        // Full workflow: get book price in PHP
        public async Task<decimal?> GetBookPriceInPesoAsync(string isbn, string accessToken)
        {
            var usdPrice = await GetBookPriceUsdAsync(isbn, accessToken);
            if (!usdPrice.HasValue) return null;

            var rate = await GetUsdToPhpRateAsync();
            return usdPrice.Value * rate;
        }
    }
}
