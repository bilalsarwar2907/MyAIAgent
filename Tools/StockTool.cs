using MyAIAgent.Configuration;
using MyAIAgent.Services;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace MyAIAgent.Tools
{
    public class StockTool : ITool
    {
        public string Name => "GetStockPrice";

        private readonly string _apiKey;
        private readonly string _baseUrl;
        private readonly IHttpClientFactory _httpClientFactory;

        public StockTool(IHttpClientFactory httpClientFactory, IOptions<AlphaVantageOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _apiKey = options.Value.ApiKey;
            _baseUrl = options.Value.BaseUrl;
        }

        public Task<string> ExecuteAsync(string input)
        {
            return FetchStockPrice(input.Trim().ToUpper());
        }

        private async Task<string> FetchStockPrice(string symbol)
        {
            try
            {
                var url = _baseUrl +
                    "?function=GLOBAL_QUOTE" +
                    "&symbol=" + symbol +
                    "&apikey=" + _apiKey;

                var http = _httpClientFactory.CreateClient("alphavantage");
                var response = await http.GetStringAsync(url);
                var json = JsonDocument.Parse(response);

                // ✅ Check for rate limit message FIRST
                if (json.RootElement.TryGetProperty("Information", out var infoMsg))
                {
                    var msg = infoMsg.GetString() ?? "";
                    if (msg.Contains("rate limit") || msg.Contains("25 requests"))
                    {
                        return "⏱️ Daily API limit reached (25 requests/day on the free plan). " +
                               "This resets in 24 hours. Try again tomorrow, or check your " +
                               "watchlist/portfolio for stocks already fetched today.";
                    }
                    return "⚠️ API notice: " + msg;
                }

                // ✅ Check for explicit error messages
                if (json.RootElement.TryGetProperty("Error Message", out var errMsg))
                {
                    return "❌ Invalid symbol '" + symbol + "'. Please check the ticker and try again.";
                }

                if (!json.RootElement.TryGetProperty("Global Quote", out var quote))
                {
                    return "Could not find stock data for: " + symbol + ". Check the symbol and try again.";
                }

                if (quote.EnumerateObject().Count() == 0)
                {
                    return "Symbol '" + symbol + "' not found. Make sure it is a valid US stock symbol like AAPL, TSLA, MSFT.";
                }

                var price = GetValue(quote, "05. price");
                var change = GetValue(quote, "09. change");
                var changePct = GetValue(quote, "10. change percent");
                var high = GetValue(quote, "03. high");
                var low = GetValue(quote, "04. low");
                var volume = GetValue(quote, "06. volume");
                var latestDay = GetValue(quote, "07. latest trading day");

                return
                    "📈 Stock: " + symbol + "\n" +
                    "💰 Price: $" + price + "\n" +
                    "📊 Change: " + change + " (" + changePct + ")\n" +
                    "📅 Date: " + latestDay + "\n" +
                    "🔺 High: $" + high + "\n" +
                    "🔻 Low: $" + low + "\n" +
                    "📦 Volume: " + volume;
            }
            catch (HttpRequestException ex)
            {
                return "🌐 Network error — could not reach the stock data service. Check your internet connection. (" + ex.Message + ")";
            }
            catch (TaskCanceledException)
            {
                return "⏱️ The request took too long and timed out. Please try again.";
            }
            catch (Exception ex)
            {
                return "⚠️ Unexpected error fetching stock price: " + ex.Message;
            }
        }

        private string GetValue(JsonElement element, string key)
        {
            if (element.TryGetProperty(key, out var value))
                return value.GetString() ?? "N/A";
            return "N/A";
        }
    }
}