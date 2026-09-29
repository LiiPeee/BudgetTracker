﻿using BudgetTracker.Core.Infrastructure.OutPut;
using BudgetTracker.Core.Infrastructure.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;


namespace BudgetTracker.Infrastructure.Services
{
    public class BacenService(IHttpClientFactory httpClientFactory, IConfiguration configuration, ILogger<BacenService> logger) : IBacenService
    {
        private readonly string _urlBase = configuration["Bacen:Url"]!;
        private readonly IHttpClientFactory _httpClientFactory = httpClientFactory;
        private readonly ILogger<BacenService> _logger = logger;

        private static readonly JsonSerializerOptions _jsonOptions = new()
        {
            PropertyNameCaseInsensitive = true
        };


        public async Task<IEnumerable<BacenOutPut>> GetHistoryCdiAsync(string from, string to)
        {
            var client = _httpClientFactory.CreateClient();

            var response = await client.GetAsync($"{_urlBase}/dados/serie/bcdata.sgs.12/dados?formato=json&dataInicial={from}&dataFinal={to}");

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError("Bacen API returned {StatusCode} for CDI history {From}..{To}", response.StatusCode, from, to);
                throw new HttpRequestException($"Bacen API returned {(int)response.StatusCode} for CDI history");
            }

            var data = await response.Content.ReadFromJsonAsync<List<BacenCdiEntry>>(_jsonOptions);

            if (data is null || data.Count == 0)
            {
                _logger.LogWarning("Bacen API returned no CDI data for {From}..{To}", from, to);
                return [];
            }

            return data.Select(e =>
               new BacenOutPut()
               {
                    Date = DateOnly.ParseExact(e.Data, "dd/MM/yyyy").ToString("yyyy-MM-dd"),
                    // O Bacen usa vírgula decimal ("0,1365"); InvariantCulture trataria a
                    // vírgula como separador de milhar e multiplicaria o valor por 1000.
                    Value = decimal.Parse(e.Valor.Replace(".", "").Replace(",", "."), CultureInfo.InvariantCulture)
               }
            );
        }
        

        public record BacenCdiEntry(
        [property: JsonPropertyName("data")] string Data,   
        [property: JsonPropertyName("valor")] string Valor  
        );
    }
}
