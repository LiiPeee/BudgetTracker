using System.Net.Http.Json;
using System.Text.Json;
using BudgetTracker.Core.Infrastructure.OutPut;
using BudgetTracker.Core.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;


namespace BudgetTracker.Infrastructure.Services
{
    public class StockMarketService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<StockMarketService> logger) : IStockMarketService
    {
        private readonly string _urlBase = configuration["BrApi:Url"]!;
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly ILogger<StockMarketService> _logger = logger;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private async Task<StockMarketResponse?> FetchPriceAsync(HttpClient client, string tick)
        {
            try
            {
                var response = await client.GetAsync($"{_urlBase}quote/{tick}");
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("BrAPI returned {StatusCode} for ticker {Ticker}", response.StatusCode, tick);
                    return null;
                }

                var result = await response.Content.ReadFromJsonAsync<BrApiResponse>(_jsonOptions);
                var price = result?.Results?.FirstOrDefault()?.RegularMarketPrice ?? 0;

                return price > 0 ? new StockMarketResponse { Ticker = tick, PriceMarket = price } : null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to fetch price for ticker {Ticker}", tick);
                return null;
            }
        }

        public async Task<List<StockMarketResponse>> GetStockByTickerAsync(List<string> ticker)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {configuration["BrApi:Key"]}");

            var results = await Task.WhenAll(ticker.Select(tick => FetchPriceAsync(client, tick)));

            return results.Where(r => r is not null).Select(r => r!).ToList();
        }

        public async Task<List<StockMarketResponse>> GetFundsByTickerAsync(List<string> ticker)
        {
            var client = _httpClientFactory.CreateClient();
            client.DefaultRequestHeaders.Add("Authorization", $"Bearer {configuration["BrApi:Key"]}");

            var results = await Task.WhenAll(ticker.Select(tick => FetchPriceAsync(client, tick)));

            return results.Where(r => r is not null).Select(r => r!).ToList();
        }
    }
}
