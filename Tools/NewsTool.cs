using MyAIAgent.Services;
using System.Text.Json;

namespace MyAIAgent.Tools
{
    public class NewsArticle
    {
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";
        public string Source { get; set; } = "";
        public string TimePublished { get; set; } = "";
        public string Summary { get; set; } = "";
        public string SentimentLabel { get; set; } = "";
        public string SentimentScore { get; set; } = "";
    }

    public class NewsTool : ITool
    {
        public string Name => "GetStockNews";

        // ✅ Same Alpha Vantage key as other stock tools
        private const string API_KEY = "RZRQ76MU2EMPJWJN";
        private const string BASE_URL = "https://www.alphavantage.co/query";

        private readonly HttpClient _httpClient;

        public NewsTool()
        {
            _httpClient = new HttpClient
            {
                Timeout = TimeSpan.FromSeconds(15)
            };
        }

        /// <summary>
        /// Input: stock symbol e.g. "AAPL"
        /// </summary>
        public string Execute(string input)
        {
            return FetchNews(input.Trim().ToUpper()).GetAwaiter().GetResult();
        }

        private async Task<string> FetchNews(string symbol)
        {
            try
            {
                var url = BASE_URL +
                    "?function=NEWS_SENTIMENT" +
                    "&tickers=" + symbol +
                    "&limit=5" +
                    "&apikey=" + API_KEY;

                var response = await _httpClient.GetStringAsync(url);
                var json = JsonDocument.Parse(response);

                // Check for rate limit
                if (json.RootElement.TryGetProperty("Information", out var infoMsg))
                {
                    var msg = infoMsg.GetString() ?? "";
                    if (msg.Contains("rate limit") || msg.Contains("25 requests"))
                    {
                        return "⏱️ Daily API limit reached (25 requests/day). News will be available again in 24 hours.";
                    }
                    return "⚠️ API notice: " + msg;
                }

                if (!json.RootElement.TryGetProperty("feed", out var feed))
                {
                    return "No recent news found for " + symbol + ".";
                }

                var articles = new List<NewsArticle>();

                foreach (var item in feed.EnumerateArray().Take(5))
                {
                    var article = new NewsArticle
                    {
                        Title = GetVal(item, "title"),
                        Url = GetVal(item, "url"),
                        Source = GetVal(item, "source"),
                        TimePublished = GetVal(item, "time_published"),
                        Summary = GetVal(item, "summary")
                    };

                    // Try to get overall sentiment for this specific ticker
                    if (item.TryGetProperty("ticker_sentiment", out var tickerSentiments))
                    {
                        foreach (var ts in tickerSentiments.EnumerateArray())
                        {
                            if (GetVal(ts, "ticker") == symbol)
                            {
                                article.SentimentLabel = GetVal(ts, "ticker_sentiment_label");
                                article.SentimentScore = GetVal(ts, "ticker_sentiment_score");
                                break;
                            }
                        }
                    }

                    articles.Add(article);
                }

                if (articles.Count == 0)
                {
                    return "No recent news found for " + symbol + ".";
                }

                return BuildSummary(symbol, articles);
            }
            catch (HttpRequestException ex)
            {
                return "🌐 Network error fetching news: " + ex.Message;
            }
            catch (Exception ex)
            {
                return "⚠️ Error fetching news: " + ex.Message;
            }
        }

        private string BuildSummary(string symbol, List<NewsArticle> articles)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== LATEST NEWS FOR " + symbol + " ===");
            sb.AppendLine();

            foreach (var a in articles)
            {
                sb.AppendLine("📰 " + a.Title);
                sb.AppendLine("   Source: " + a.Source);
                if (!string.IsNullOrEmpty(a.SentimentLabel))
                {
                    sb.AppendLine("   Sentiment: " + a.SentimentLabel + " (" + a.SentimentScore + ")");
                }
                sb.AppendLine("   Summary: " + a.Summary);
                sb.AppendLine("   Link: " + a.Url);
                sb.AppendLine();
            }

            return sb.ToString();
        }

        private string GetVal(JsonElement element, string key)
        {
            if (element.TryGetProperty(key, out var value))
            {
                if (value.ValueKind == JsonValueKind.String) return value.GetString() ?? "";
                if (value.ValueKind == JsonValueKind.Number) return value.GetRawText();
            }
            return "";
        }
    }
}