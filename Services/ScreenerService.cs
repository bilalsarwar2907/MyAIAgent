using MyAIAgent.Services;

namespace MyAIAgent.Services
{
    /// <summary>
    /// RSI Candidate Screener — v1 (lightweight, fast)
    ///
    /// Applies the validated Finding #1 rule:
    ///   Exclude stocks with 10-year B&H return > 300% (strong-trend exclusion).
    ///
    /// Returns per-stock:
    ///   symbol, sector, 10y B&H return, trend bucket, current RSI, pass/fail
    ///
    /// V2 TODO: Add historical advantage column (full backtest per stock,
    ///          ~2–3 min load time — offer as "Deep Analysis" button on demand).
    /// </summary>
    public class ScreenerService
    {
        private readonly HistoricalDataService _data;

        // Sector lookup — reverse-index from StockUniverse.BySector
        private static readonly Dictionary<string, string> _symbolToSector =
            StockUniverse.BySector
                .SelectMany(kv => kv.Value.Select(sym => (sym, kv.Key)))
                .ToDictionary(t => t.sym, t => t.Key);

        public ScreenerService(HistoricalDataService data)
        {
            _data = data;
        }

        /// <summary>
        /// Runs the screener against all 59 stocks.
        /// Fetches 10y price history per symbol, calculates B&H return and current RSI.
        /// Applies the >300% exclusion rule from Finding #1.
        /// </summary>
        public async Task<ScreenerResult> RunAsync(IEnumerable<string> symbols)
        {
            var candidates = new List<ScreenerStock>();
            var excluded = new List<ScreenerStock>();
            var errors = new List<string>();

            foreach (var symbol in symbols)
            {
                try
                {
                    var bars = await _data.GetDailyHistoryAsync(symbol);
                    if (bars.Count < 20)
                    {
                        errors.Add($"{symbol}: insufficient data ({bars.Count} bars)");
                        continue;
                    }

                    // ── 10-year Buy & Hold return ──────────────────────────────
                    var firstClose = bars.First().Close;
                    var lastClose = bars.Last().Close;
                    decimal bahReturn = Math.Round(((lastClose - firstClose) / firstClose) * 100, 1);

                    // ── Trend bucket (same thresholds as factor research) ──────
                    string trendBucket = bahReturn < 100 ? "Weak (<100%)"
                                       : bahReturn < 300 ? "Medium (100–300%)"
                                                           : "Strong (>300%)";

                    // ── Current RSI (last 14 days, needs 14+ bars of close) ───
                    // TechnicalIndicators.CalculateRsiSeries returns a full series;
                    // we take the last non-null value as the "current" RSI.
                    var closes = bars.Select(b => b.Close).ToList();
                    var rsiSeries = TechnicalIndicators.CalculateRsiSeries(closes, period: 14);
                    decimal? currentRsi = rsiSeries.LastOrDefault(r => r.HasValue);

                    // ── Finding #1 exclusion rule ──────────────────────────────
                    bool passes = bahReturn <= 300;

                    var stock = new ScreenerStock
                    {
                        Symbol = symbol,
                        Sector = _symbolToSector.TryGetValue(symbol, out var sec) ? sec : "unknown",
                        BahReturn = bahReturn,
                        TrendBucket = trendBucket,
                        CurrentRsi = currentRsi.HasValue ? Math.Round(currentRsi.Value, 1) : null,
                        Passes = passes,
                        ExcludeReason = passes ? null : "Strong trend (>300% 10y return) — Finding #1 rule"
                    };

                    if (passes)
                        candidates.Add(stock);
                    else
                        excluded.Add(stock);
                }
                catch (Exception ex)
                {
                    errors.Add($"{symbol}: {ex.Message}");
                }

                // Small delay to avoid rate-limiting Yahoo Finance
                await Task.Delay(200);
            }

            return new ScreenerResult
            {
                TotalScreened = candidates.Count + excluded.Count,
                TotalCandidates = candidates.Count,
                TotalExcluded = excluded.Count,
                ExclusionRule = "Finding #1: exclude stocks with >300% 10-year B&H return",
                OversoldCount = candidates.Count(s => s.CurrentRsi.HasValue && s.CurrentRsi < 30),
                GeneratedAt = DateTime.UtcNow,
                Candidates = candidates.OrderBy(s => s.CurrentRsi ?? 999).ToList(),
                Excluded = excluded.OrderByDescending(s => s.BahReturn).ToList(),
                Errors = errors
            };
        }
    }

    public class ScreenerResult
    {
        public int TotalScreened { get; set; }
        public int TotalCandidates { get; set; }
        public int TotalExcluded { get; set; }
        public string ExclusionRule { get; set; } = "";
        public int OversoldCount { get; set; }
        public DateTime GeneratedAt { get; set; }
        public List<ScreenerStock> Candidates { get; set; } = new();
        public List<ScreenerStock> Excluded { get; set; } = new();
        public List<string> Errors { get; set; } = new();
    }

    public class ScreenerStock
    {
        public string Symbol { get; set; } = "";
        public string Sector { get; set; } = "";
        public decimal BahReturn { get; set; }
        public string TrendBucket { get; set; } = "";
        public decimal? CurrentRsi { get; set; }
        public bool Passes { get; set; }
        public string? ExcludeReason { get; set; }
    }
}