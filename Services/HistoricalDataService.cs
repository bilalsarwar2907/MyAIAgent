using System.Globalization;
using System.Text.Json;

// ═══ LOCKED BASE: HistoricalDataService ═══
// Verified correct as of June 2026 audit.
//
// Fetches multi-year daily OHLCV from Yahoo Finance v8 (free, no daily cap).
// Used exclusively for backtesting -- live quotes still use Alpha Vantage.
//
// Key notes:
// - Requires browser-like User-Agent header (Yahoo 401s without it)
// - Returns bars oldest-first (required by TechnicalIndicators)
// - Null OHLCV entries from Yahoo are skipped (non-trading days)
// - GetDailyHistoryForManyAsync silently swallows per-symbol errors;
//   check returned dictionary keys vs input list to detect failures
//
// Safe to build on: GetDailyHistoryAsync, GetDailyHistoryForManyAsync,
//                   GetDailyHistoryRangeAsync
// ══════════════════════════════════════════
namespace MyAIAgent.Services
{
    public class DailyBar
    {
        public DateTime Date { get; set; }
        public decimal Open { get; set; }
        public decimal High { get; set; }
        public decimal Low { get; set; }
        public decimal Close { get; set; }
        public decimal Volume { get; set; }
    }

    /// <summary>
    /// Fetches multi-year daily price history from Yahoo Finance v8 JSON API.
    /// Free, no API key, no 25-requests/day cap.
    /// Used ONLY for backtesting — live quotes still go through Alpha Vantage.
    /// </summary>
    public class HistoricalDataService
    {
        private readonly HttpClient _httpClient;

        public HistoricalDataService()
        {
            _httpClient = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };

            _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                "AppleWebKit/537.36 (KHTML, like Gecko) " +
                "Chrome/124.0 Safari/537.36");
        }

        /// <summary>
        /// LOCKED — do not modify.
        /// Returns daily bars for a US-listed symbol, oldest bar first.
        /// Range = 10y (2016–2026). Used for all First Report results.
        /// </summary>
        public async Task<List<DailyBar>> GetDailyHistoryAsync(string symbol)
        {
            var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{symbol.ToUpper()}" +
                      "?interval=1d&range=10y";

            string json;
            try
            {
                json = await _httpClient.GetStringAsync(url);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Could not reach Yahoo Finance for '{symbol}': {ex.Message}");
            }

            return ParseYahooJson(json, symbol);
        }

        /// <summary>
        /// NEW — multi-period validation overload.
        /// Fetches bars between fromDate and toDate using Yahoo's period1/period2
        /// unix timestamp params instead of the rolling range shorthand.
        /// Used for 2006–2016 backtest. Does NOT affect the locked 10y method.
        /// </summary>
        public async Task<List<DailyBar>> GetDailyHistoryRangeAsync(
            string symbol,
            DateTime fromDate,
            DateTime toDate)
        {
            var period1 = new DateTimeOffset(fromDate, TimeSpan.Zero).ToUnixTimeSeconds();
            var period2 = new DateTimeOffset(toDate, TimeSpan.Zero).ToUnixTimeSeconds();

            var url = $"https://query1.finance.yahoo.com/v8/finance/chart/{symbol.ToUpper()}" +
                      $"?interval=1d&period1={period1}&period2={period2}";

            string json;
            try
            {
                json = await _httpClient.GetStringAsync(url);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    $"Could not reach Yahoo Finance for '{symbol}': {ex.Message}");
            }

            return ParseYahooJson(json, symbol);
        }

        /// <summary>
        /// Batch fetch. Symbols that fail are skipped (not thrown) — check the
        /// returned dictionary's keys vs. the input list to see what was missed.
        /// </summary>
        public async Task<Dictionary<string, List<DailyBar>>> GetDailyHistoryForManyAsync(
            IEnumerable<string> symbols,
            int delayMs = 300)
        {
            var result = new Dictionary<string, List<DailyBar>>();

            foreach (var symbol in symbols)
            {
                try
                {
                    var bars = await GetDailyHistoryAsync(symbol);
                    if (bars.Count > 0)
                        result[symbol.ToUpper()] = bars;
                }
                catch { /* intentionally swallowed — see doc comment */ }

                await Task.Delay(delayMs);
            }

            return result;
        }

        private List<DailyBar> ParseYahooJson(string json, string symbol)
        {
            var bars = new List<DailyBar>();

            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            if (root.TryGetProperty("chart", out var chart) &&
                chart.TryGetProperty("error", out var err) &&
                err.ValueKind != JsonValueKind.Null)
            {
                var msg = err.TryGetProperty("description", out var d) ? d.GetString() : "unknown error";
                throw new InvalidOperationException($"Yahoo Finance error for '{symbol}': {msg}");
            }

            var result = chart.GetProperty("result")[0];

            var timestamps = result.GetProperty("timestamp").EnumerateArray().ToList();
            var quote = result
                .GetProperty("indicators")
                .GetProperty("quote")[0];

            var opens = quote.GetProperty("open").EnumerateArray().ToList();
            var highs = quote.GetProperty("high").EnumerateArray().ToList();
            var lows = quote.GetProperty("low").EnumerateArray().ToList();
            var closes = quote.GetProperty("close").EnumerateArray().ToList();
            var volumes = quote.GetProperty("volume").EnumerateArray().ToList();

            for (int i = 0; i < timestamps.Count; i++)
            {
                if (closes[i].ValueKind == JsonValueKind.Null) continue;

                var date = DateTimeOffset.FromUnixTimeSeconds(timestamps[i].GetInt64()).Date;

                bars.Add(new DailyBar
                {
                    Date = date,
                    Open = opens[i].ValueKind != JsonValueKind.Null ? opens[i].GetDecimal() : 0,
                    High = highs[i].ValueKind != JsonValueKind.Null ? highs[i].GetDecimal() : 0,
                    Low = lows[i].ValueKind != JsonValueKind.Null ? lows[i].GetDecimal() : 0,
                    Close = closes[i].GetDecimal(),
                    Volume = volumes[i].ValueKind != JsonValueKind.Null ? volumes[i].GetDecimal() : 0,
                });
            }

            return bars.OrderBy(b => b.Date).ToList();
        }
    }
}