using MyAIAgent.Configuration;
using MyAIAgent.Services;
using Microsoft.Extensions.Options;
using System.Text.Json;
using System.Globalization;

namespace MyAIAgent.Tools
{
    public class PricePoint
    {
        public DateTime Date { get; set; }
        public decimal Close { get; set; }
    }

    public class Trade
    {
        public DateTime BuyDate { get; set; }
        public decimal BuyPrice { get; set; }
        public DateTime SellDate { get; set; }
        public decimal SellPrice { get; set; }
        public decimal ReturnPercent { get; set; }
    }

    public class BacktestResult
    {
        public string Symbol { get; set; } = "";
        public string Strategy { get; set; } = "";
        public int TotalTrades { get; set; }
        public int WinningTrades { get; set; }
        public int LosingTrades { get; set; }
        public decimal WinRate { get; set; }
        public decimal TotalReturnPercent { get; set; }
        public decimal BuyAndHoldReturnPercent { get; set; }
        public List<Trade> Trades { get; set; } = new();
        public string Error { get; set; } = "";
    }

    /// <summary>
    /// Tests a simple RSI-based strategy against real historical data.
    /// This answers "would this rule have made money in the past?" —
    /// NOT a guarantee of future performance.
    /// </summary>
    public class BacktestTool : ITool
    {
        public string Name => "BacktestStrategy";

        private readonly string _apiKey;
        private readonly string _baseUrl;
        private readonly IHttpClientFactory _httpClientFactory;

        public BacktestTool(IHttpClientFactory httpClientFactory, IOptions<AlphaVantageOptions> options)
        {
            _httpClientFactory = httpClientFactory;
            _apiKey = options.Value.ApiKey;
            _baseUrl = options.Value.BaseUrl;
        }

        /// <summary>
        /// Input: one symbol "AAPL" or several comma-separated "AAPL,MSFT,TSLA".
        /// Strategy is fixed for now: buy when RSI < 30, sell when RSI > 70.
        /// Each symbol costs 1 API request — keep batches small (5 or fewer)
        /// to avoid burning through the daily free-tier limit in one call.
        /// </summary>
        public async Task<string> ExecuteAsync(string input)
        {
            var symbols = input.Split(',')
                .Select(s => s.Trim().ToUpper())
                .Where(s => !string.IsNullOrEmpty(s))
                .Take(5) // hard cap to protect the daily quota
                .ToList();

            if (symbols.Count == 1)
            {
                var single = await RunBacktest(symbols[0]);
                return FormatResult(single);
            }

            var results = new List<BacktestResult>();
            for (int i = 0; i < symbols.Count; i++)
            {
                results.Add(await RunBacktest(symbols[i]));

                // Small delay between symbols to be polite to the API
                if (i < symbols.Count - 1)
                    await Task.Delay(1200);
            }

            return FormatBatchSummary(results);
        }
        /// <summary>
        /// Predefined sector groups for comparison testing.
        /// 3 stocks each to keep API usage low (3 requests per sector tested).
        /// </summary>
        public Task<string> ExecuteSectorAsync(string sectorName)
        {
            var sectors = new Dictionary<string, string>
    {
        { "banks", "JPM,BAC,WFC" },
        { "auto", "TSLA,F,GM" },
        { "pharma", "PFE,JNJ,MRK" }
    };

            var key = sectorName.Trim().ToLower();

            if (!sectors.ContainsKey(key))
            {
                return Task.FromResult(
                    "⚠️ Unknown sector '" + sectorName + "'. Available sectors: banks, auto, pharma.");
            }

            return ExecuteAsync(sectors[key]);
        }

        private async Task<BacktestResult> RunBacktest(string symbol)
        {
            var result = new BacktestResult
            {
                Symbol = symbol,
                Strategy = "Buy when RSI < 30 (oversold), Sell when RSI > 70 (overbought)"
            };

            try
            {
                // Fetch ~2 years of daily prices
                var priceUrl = _baseUrl +
                    "?function=TIME_SERIES_DAILY&symbol=" + symbol +
                    "&outputsize=compact&apikey=" + _apiKey;

                var http = _httpClientFactory.CreateClient("alphavantage");
                var priceJson = await http.GetStringAsync(priceUrl);
                var priceDoc = JsonDocument.Parse(priceJson);

                if (priceDoc.RootElement.TryGetProperty("Information", out var infoMsg))
                {
                    var msg = infoMsg.GetString() ?? "";
                    result.Error = msg.Contains("rate limit") || msg.Contains("25 requests")
                        ? "Daily API limit reached (25 requests/day). Try again in 24 hours."
                        : "API notice: " + msg;
                    return result;
                }

                if (!priceDoc.RootElement.TryGetProperty("Time Series (Daily)", out var series))
                {
                    result.Error = "No historical data found for " + symbol + ".";
                    return result;
                }

                var prices = new List<PricePoint>();
                foreach (var day in series.EnumerateObject())
                {
                    var dateStr = day.Name;
                    var close = day.Value.GetProperty("4. close").GetString();

                    if (DateTime.TryParse(dateStr, CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                        && decimal.TryParse(close, CultureInfo.InvariantCulture, out var closePrice))
                    {
                        prices.Add(new PricePoint { Date = date, Close = closePrice });
                    }
                }

                // Sort oldest to newest, take last ~500 trading days (~2 years)
                prices = prices.OrderBy(p => p.Date).TakeLast(500).ToList();

                if (prices.Count < 30)
                {
                    result.Error = "Not enough historical data to backtest " + symbol + ".";
                    return result;
                }

                // Calculate RSI(14) for each day
                var rsiValues = CalculateRsiSeries(prices, 14);

                // Run the strategy: buy when RSI < 30, sell when RSI > 70
                var trades = new List<Trade>();
                bool holding = false;
                DateTime buyDate = default;
                decimal buyPrice = 0;

                for (int i = 14; i < prices.Count; i++)
                {
                    var rsi = rsiValues[i];

                    if (!holding && rsi < 30)
                    {
                        holding = true;
                        buyDate = prices[i].Date;
                        buyPrice = prices[i].Close;
                    }
                    else if (holding && rsi > 70)
                    {
                        var sellDate = prices[i].Date;
                        var sellPrice = prices[i].Close;
                        var returnPct = ((sellPrice - buyPrice) / buyPrice) * 100;

                        trades.Add(new Trade
                        {
                            BuyDate = buyDate,
                            BuyPrice = buyPrice,
                            SellDate = sellDate,
                            SellPrice = sellPrice,
                            ReturnPercent = returnPct
                        });

                        holding = false;
                    }
                }

                result.Trades = trades;
                result.TotalTrades = trades.Count;
                result.WinningTrades = trades.Count(t => t.ReturnPercent > 0);
                result.LosingTrades = trades.Count(t => t.ReturnPercent <= 0);
                result.WinRate = trades.Count > 0
                    ? Math.Round((decimal)result.WinningTrades / trades.Count * 100, 1)
                    : 0;

                // Compound return across all trades (assumes reinvesting each time)
                decimal compoundedReturn = 1;
                foreach (var t in trades)
                {
                    compoundedReturn *= (1 + (t.ReturnPercent / 100));
                }
                result.TotalReturnPercent = Math.Round((compoundedReturn - 1) * 100, 2);

                // Compare against simple buy-and-hold over the same period
                var firstPrice = prices.First().Close;
                var lastPrice = prices.Last().Close;
                result.BuyAndHoldReturnPercent = Math.Round(((lastPrice - firstPrice) / firstPrice) * 100, 2);

                return result;
            }
            catch (Exception ex)
            {
                result.Error = "Backtest error: " + ex.Message;
                return result;
            }
        }

        /// <summary>
        /// Calculates RSI for every day in the series using Wilder's smoothing method.
        /// </summary>
        private List<decimal> CalculateRsiSeries(List<PricePoint> prices, int period)
        {
            var rsi = new List<decimal>(new decimal[prices.Count]);

            decimal avgGain = 0, avgLoss = 0;

            for (int i = 1; i <= period && i < prices.Count; i++)
            {
                var change = prices[i].Close - prices[i - 1].Close;
                if (change > 0) avgGain += change;
                else avgLoss += Math.Abs(change);
            }

            avgGain /= period;
            avgLoss /= period;

            for (int i = period + 1; i < prices.Count; i++)
            {
                var change = prices[i].Close - prices[i - 1].Close;
                var gain = change > 0 ? change : 0;
                var loss = change < 0 ? Math.Abs(change) : 0;

                avgGain = ((avgGain * (period - 1)) + gain) / period;
                avgLoss = ((avgLoss * (period - 1)) + loss) / period;

                var rs = avgLoss == 0 ? 100 : avgGain / avgLoss;
                rsi[i] = avgLoss == 0 ? 100 : 100 - (100 / (1 + rs));
            }

            return rsi;
        }

        /// <summary>
        /// Builds a side-by-side comparison table for multiple symbols,
        /// so patterns (or lack of patterns) are visible at a glance.
        /// </summary>
        private string FormatBatchSummary(List<BacktestResult> results)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== BACKTEST COMPARISON ===");
            sb.AppendLine("Strategy: Buy when RSI < 30, Sell when RSI > 70");
            sb.AppendLine("Period: last ~100 trading days (free tier limit)");
            sb.AppendLine();

            int beatBuyHold = 0;
            int validResults = 0;

            foreach (var r in results)
            {
                if (!string.IsNullOrEmpty(r.Error))
                {
                    sb.AppendLine("❌ " + r.Symbol + ": " + r.Error);
                    sb.AppendLine();
                    continue;
                }

                validResults++;
                var outcome = r.TotalReturnPercent > r.BuyAndHoldReturnPercent ? "BEAT buy-and-hold" : "underperformed buy-and-hold";
                if (r.TotalReturnPercent > r.BuyAndHoldReturnPercent) beatBuyHold++;

                sb.AppendLine("📊 " + r.Symbol);
                sb.AppendLine("   Trades: " + r.TotalTrades + " (win rate: " + r.WinRate + "%)");
                sb.AppendLine("   Strategy return: " + r.TotalReturnPercent + "%");
                sb.AppendLine("   Buy-and-hold return: " + r.BuyAndHoldReturnPercent + "%");
                sb.AppendLine("   Result: " + outcome);
                sb.AppendLine();
            }

            sb.AppendLine("=== SUMMARY ===");
            if (validResults > 0)
            {
                sb.AppendLine(beatBuyHold + " out of " + validResults + " symbols beat simple buy-and-hold.");
                var totalTradesAcrossAll = results.Sum(r => r.TotalTrades);
                sb.AppendLine("Total trades across all symbols: " + totalTradesAcrossAll +
                    (totalTradesAcrossAll < 15 ? " — this is a small sample, treat conclusions cautiously." : ""));
            }
            else
            {
                sb.AppendLine("No valid results returned — check errors above.");
            }

            sb.AppendLine();
            sb.AppendLine("⚠️ Past performance does not predict future results. This is historical simulation only, not financial advice.");

            return sb.ToString();
        }

        private string FormatResult(BacktestResult r)
        {
            if (!string.IsNullOrEmpty(r.Error))
            {
                return "⚠️ " + r.Error;
            }

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== BACKTEST RESULT: " + r.Symbol + " ===");
            sb.AppendLine("Strategy: " + r.Strategy);
            sb.AppendLine("Period: last ~2 years of daily data");
            sb.AppendLine();
            sb.AppendLine("Total trades: " + r.TotalTrades);
            sb.AppendLine("Winning trades: " + r.WinningTrades);
            sb.AppendLine("Losing trades: " + r.LosingTrades);
            sb.AppendLine("Win rate: " + r.WinRate + "%");
            sb.AppendLine();
            sb.AppendLine("Strategy total return (compounded): " + r.TotalReturnPercent + "%");
            sb.AppendLine("Buy-and-hold return (same period): " + r.BuyAndHoldReturnPercent + "%");
            sb.AppendLine();

            if (r.TotalTrades == 0)
            {
                sb.AppendLine("No trades were triggered — RSI never crossed below 30 in this period for " + r.Symbol + ".");
            }
            else if (r.TotalReturnPercent > r.BuyAndHoldReturnPercent)
            {
                sb.AppendLine("This strategy outperformed simply buying and holding over this period.");
            }
            else
            {
                sb.AppendLine("This strategy underperformed simply buying and holding over this period.");
            }

            sb.AppendLine();
            sb.AppendLine("⚠️ Past performance does not predict future results. This is historical simulation only, not financial advice.");

            return sb.ToString();
        }
    }
}