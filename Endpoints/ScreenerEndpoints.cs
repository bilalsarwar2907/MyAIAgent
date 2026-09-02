using MyAIAgent.Common;
using MyAIAgent.Services;

namespace MyAIAgent.Endpoints
{
    /// <summary>
    /// RSI candidate screener over the full universe, plus a single-symbol RSI +
    /// live-price lookup used by the Watchlist / Portfolio / Alerts panels.
    /// </summary>
    public static class ScreenerEndpoints
    {
        public static void MapScreenerEndpoints(this WebApplication app)
        {
            app.MapGet("/api/screener/rsi-candidates", async (IScreenerService screener) =>
            {
                var result = await screener.RunAsync(StockUniverse.All);
                return Results.Json(result);
            });

            app.MapGet("/api/screener/rsi/{symbol}", async (string symbol, IHistoricalDataService data, IHttpClientFactory httpClientFactory) =>
            {
                var sym = symbol.ToUpper().Trim();

                try
                {
                    var bars = await data.GetDailyHistoryAsync(sym);

                    if (bars.Count < 20)
                        return Results.Json(new
                        {
                            symbol = sym,
                            currentRsi = (decimal?)null,
                            currentPrice = (decimal?)null,
                            trendBucket = (string?)null,
                            bahReturn = (decimal?)null,
                            passes = (bool?)null,
                            error = $"Insufficient data ({bars.Count} bars)"
                        });

                    // 10-year B&H return
                    var firstClose = bars.First().Close;
                    var lastClose = bars.Last().Close;
                    decimal bahReturn = Math.Round(((lastClose - firstClose) / firstClose) * 100, 1);

                    string trendBucket = TrendBucket.For(bahReturn);

                    // Current RSI from historical closes
                    var closes = bars.Select(b => b.Close).ToList();
                    var rsiSeries = TechnicalIndicators.CalculateRsiSeries(closes, period: 14);
                    decimal? currentRsi = rsiSeries.LastOrDefault(r => r.HasValue);
                    if (currentRsi.HasValue) currentRsi = Math.Round(currentRsi.Value, 1);

                    // Finding #1 exclusion
                    bool passes = bahReturn <= 300;

                    // Live market price from the Yahoo quote endpoint (historical bars
                    // give OHLC but not the current price).
                    decimal? currentPrice = null;
                    try
                    {
                        var http = httpClientFactory.CreateClient("yahoo");
                        var quoteUrl = $"https://query1.finance.yahoo.com/v8/finance/chart/{sym}?interval=1d&range=1d";
                        var quoteRes = await http.GetAsync(quoteUrl);
                        if (quoteRes.IsSuccessStatusCode)
                        {
                            var quoteJson = await quoteRes.Content.ReadAsStringAsync();
                            var metaMatch = System.Text.RegularExpressions.Regex.Match(
                                quoteJson, @"""regularMarketPrice""\s*:\s*([\d.]+)");
                            if (metaMatch.Success &&
                                decimal.TryParse(metaMatch.Groups[1].Value,
                                    System.Globalization.NumberStyles.Any,
                                    System.Globalization.CultureInfo.InvariantCulture,
                                    out var price))
                            {
                                currentPrice = Math.Round(price, 2);
                            }
                        }
                    }
                    catch
                    {
                        // currentPrice stays null — frontend shows — instead of crashing
                    }

                    return Results.Json(new
                    {
                        symbol,
                        currentRsi,
                        currentPrice,
                        trendBucket,
                        bahReturn,
                        passes,
                        excludeReason = passes ? null : "Strong trend (>300% 10y return) — Finding #1 rule"
                    });
                }
                catch (Exception ex)
                {
                    return Results.Json(new
                    {
                        symbol = sym,
                        currentRsi = (decimal?)null,
                        currentPrice = (decimal?)null,
                        trendBucket = (string?)null,
                        bahReturn = (decimal?)null,
                        passes = (bool?)null,
                        error = ex.Message
                    });
                }
            });
        }
    }
}
