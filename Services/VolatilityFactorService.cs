namespace MyAIAgent.Services
{
    /// <summary>
    /// Per-stock result enriched with volatility metrics.
    /// </summary>
    public class VolatilityStockResult
    {
        public string Symbol { get; set; } = "";
        public decimal AnnualisedVolatility { get; set; }   // std dev of daily returns × √252, as %
        public string VolatilityBucket { get; set; } = "";  // "Low", "Medium", "High"
        public decimal BahReturn { get; set; }
        public decimal RsiReturn { get; set; }
        public decimal Advantage { get; set; }              // RsiReturn - BahReturn
        public bool Beat { get; set; }
        public int Trades { get; set; }
        public decimal WinRate { get; set; }
        public string Error { get; set; } = "";
    }

    /// <summary>
    /// Summary for one volatility bucket.
    /// </summary>
    public class VolatilityBucketSummary
    {
        public string Bucket { get; set; } = "";
        public int Total { get; set; }
        public int BeatCount { get; set; }
        public decimal BeatRate { get; set; }               // %
        public decimal MedianAdvantage { get; set; }        // median of (RsiReturn - BahReturn)
        public decimal AvgVolatility { get; set; }          // avg annualised vol in this bucket
    }

    /// <summary>
    /// Full result returned to the controller / frontend.
    /// </summary>
    public class VolatilityFactorResult
    {
        public string Hypothesis { get; set; } =
            "RSI mean-reversion works better on low-volatility stocks than high-volatility stocks.";
        public string Period { get; set; } = "";
        public List<VolatilityBucketSummary> Buckets { get; set; } = new();
        public List<VolatilityStockResult> PerStock { get; set; } = new();
        public string Finding { get; set; } = "";
        public string Error { get; set; } = "";
    }

    /// <summary>
    /// Tests whether daily-return volatility predicts RSI strategy effectiveness.
    ///
    /// Volatility = annualised standard deviation of daily log-returns (×100 to get %).
    /// Bucket thresholds:
    ///   Low    : vol &lt; 25%   (stable blue-chips, utilities, etc.)
    ///   Medium : 25% ≤ vol &lt; 50%  (typical mid-cap growth)
    ///   High   : vol ≥ 50%   (speculative, meme stocks, small-caps)
    ///
    /// These thresholds are fixed before the run and do NOT change based on results
    /// (avoids look-ahead bias / data-snooping).
    ///
    /// Uses the LOCKED BacktestEngine — no changes to the core simulation.
    /// </summary>
    public class VolatilityFactorService
    {
        private readonly HistoricalDataService _data;
        private readonly BacktestEngine _engine;

        // Bucket thresholds (annualised %, decided before seeing results)
        private const decimal LowVolCeiling = 25m;
        private const decimal MediumVolCeiling = 50m;

        public VolatilityFactorService(HistoricalDataService data, BacktestEngine engine)
        {
            _data = data;
            _engine = engine;
        }

        /// <summary>
        /// Primary run: uses full 10-year history (2016–2026).
        /// </summary>
        public async Task<VolatilityFactorResult> RunAsync(IEnumerable<string> symbols)
        {
            var result = new VolatilityFactorResult { Period = "2016–2026" };
            var perStock = new List<VolatilityStockResult>();

            foreach (var symbol in symbols)
            {
                var stock = await ProcessSymbolAsync(symbol, null, null);
                perStock.Add(stock);
            }

            result.PerStock = perStock.OrderBy(s => s.VolatilityBucket).ThenBy(s => s.Symbol).ToList();
            result.Buckets = BuildBuckets(perStock);
            result.Finding = BuildFinding(result.Buckets);
            return result;
        }

        /// <summary>
        /// Validation run: uses a specific date range (e.g. 2006–2016).
        /// Same bucketing thresholds, same backtest engine.
        /// </summary>
        public async Task<VolatilityFactorResult> RunRangeAsync(
            IEnumerable<string> symbols,
            DateTime fromDate,
            DateTime toDate)
        {
            var label = $"{fromDate.Year}–{toDate.Year}";
            var result = new VolatilityFactorResult { Period = label };
            var perStock = new List<VolatilityStockResult>();

            foreach (var symbol in symbols)
            {
                var stock = await ProcessSymbolAsync(symbol, fromDate, toDate);
                perStock.Add(stock);
            }

            result.PerStock = perStock.OrderBy(s => s.VolatilityBucket).ThenBy(s => s.Symbol).ToList();
            result.Buckets = BuildBuckets(perStock);
            result.Finding = BuildFinding(result.Buckets);
            return result;
        }

        // ── Internal helpers ─────────────────────────────────────────────────

        private async Task<VolatilityStockResult> ProcessSymbolAsync(
            string symbol,
            DateTime? fromDate,
            DateTime? toDate)
        {
            var stock = new VolatilityStockResult { Symbol = symbol.ToUpper() };

            try
            {
                // 1. Fetch bars
                List<DailyBar> bars;
                if (fromDate.HasValue && toDate.HasValue)
                    bars = await _data.GetDailyHistoryRangeAsync(symbol, fromDate.Value, toDate.Value);
                else
                    bars = await _data.GetDailyHistoryAsync(symbol);

                if (bars.Count < 60)
                {
                    stock.Error = $"Not enough data ({bars.Count} days)";
                    stock.VolatilityBucket = "Unknown";
                    return stock;
                }

                // 2. Compute annualised volatility from daily log-returns
                stock.AnnualisedVolatility = CalculateAnnualisedVolatility(bars);
                stock.VolatilityBucket = AssignBucket(stock.AnnualisedVolatility);

                // 3. Run RSI backtest via locked engine
                SymbolBacktestResult backtest;
                if (fromDate.HasValue && toDate.HasValue)
                    backtest = await _engine.RunSingleRangeAsync(symbol, fromDate.Value, toDate.Value);
                else
                    backtest = await _engine.RunSingleAsync(symbol);

                if (!string.IsNullOrEmpty(backtest.Error))
                {
                    stock.Error = backtest.Error;
                    return stock;
                }

                stock.BahReturn = backtest.BuyAndHoldReturnPercent;
                stock.RsiReturn = backtest.TotalReturnPercent;
                stock.Advantage = Math.Round(backtest.TotalReturnPercent - backtest.BuyAndHoldReturnPercent, 2);
                stock.Beat = backtest.TotalReturnPercent > backtest.BuyAndHoldReturnPercent;
                stock.Trades = backtest.TotalTrades;
                stock.WinRate = backtest.WinRate;
            }
            catch (Exception ex)
            {
                stock.Error = ex.Message;
                stock.VolatilityBucket = "Unknown";
            }

            return stock;
        }

        /// <summary>
        /// Annualised volatility = std dev of daily log-returns × √252 × 100.
        /// Uses population std dev (divide by N) consistent with most quant literature.
        /// </summary>
        private static decimal CalculateAnnualisedVolatility(List<DailyBar> bars)
        {
            // Daily log-returns (using decimal for consistency with the rest of the engine)
            var logReturns = new List<double>();
            for (int i = 1; i < bars.Count; i++)
            {
                if (bars[i - 1].Close <= 0 || bars[i].Close <= 0) continue;
                logReturns.Add(Math.Log((double)(bars[i].Close / bars[i - 1].Close)));
            }

            if (logReturns.Count < 2) return 0;

            double mean = logReturns.Average();
            double variance = logReturns.Sum(r => (r - mean) * (r - mean)) / logReturns.Count;
            double dailyStdDev = Math.Sqrt(variance);
            double annualised = dailyStdDev * Math.Sqrt(252) * 100;

            return Math.Round((decimal)annualised, 2);
        }

        private static string AssignBucket(decimal annualisedVol) =>
            annualisedVol < LowVolCeiling ? "Low (<25%)" :
            annualisedVol < MediumVolCeiling ? "Medium (25–50%)" :
                                               "High (>50%)";

        private static List<VolatilityBucketSummary> BuildBuckets(List<VolatilityStockResult> perStock)
        {
            var bucketOrder = new[] { "Low (<25%)", "Medium (25–50%)", "High (>50%)" };
            var result = new List<VolatilityBucketSummary>();

            foreach (var bucketName in bucketOrder)
            {
                var stocks = perStock
                    .Where(s => s.VolatilityBucket == bucketName && string.IsNullOrEmpty(s.Error))
                    .ToList();

                if (stocks.Count == 0) continue;

                var advantages = stocks.Select(s => s.Advantage).OrderBy(x => x).ToList();
                int mid = advantages.Count / 2;
                decimal median = advantages.Count % 2 == 0
                    ? Math.Round((advantages[mid - 1] + advantages[mid]) / 2, 1)
                    : Math.Round(advantages[mid], 1);

                result.Add(new VolatilityBucketSummary
                {
                    Bucket = bucketName,
                    Total = stocks.Count,
                    BeatCount = stocks.Count(s => s.Beat),
                    BeatRate = stocks.Count > 0
                        ? Math.Round((decimal)stocks.Count(s => s.Beat) / stocks.Count * 100, 1)
                        : 0,
                    MedianAdvantage = median,
                    AvgVolatility = Math.Round(stocks.Average(s => s.AnnualisedVolatility), 1)
                });
            }

            return result;
        }

        private static string BuildFinding(List<VolatilityBucketSummary> buckets)
        {
            var low = buckets.FirstOrDefault(b => b.Bucket.StartsWith("Low"));
            var high = buckets.FirstOrDefault(b => b.Bucket.StartsWith("High"));

            if (low == null || high == null)
                return "Not enough data across buckets to draw a conclusion.";

            decimal gap = low.BeatRate - high.BeatRate;
            bool hypothesisSupported = low.BeatRate > high.BeatRate && gap >= 20;

            if (hypothesisSupported)
                return $"Hypothesis supported. RSI beat buy-and-hold in {low.BeatRate}% of low-volatility stocks " +
                       $"vs {high.BeatRate}% of high-volatility stocks — a {gap:0.0}pp gap. " +
                       $"Volatility appears to be a meaningful predictor of RSI effectiveness.";

            return $"Hypothesis not supported. Low-volatility win rate ({low.BeatRate}%) vs " +
                   $"high-volatility win rate ({high.BeatRate}%) — gap of {gap:0.0}pp is insufficient " +
                   $"to claim volatility reliably predicts RSI effectiveness.";
        }
    }
}