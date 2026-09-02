using MyAIAgent.Common;

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
    public class ScreenerService : IScreenerService
    {
        private readonly IHistoricalDataService _data;

        // Sector lookup — reverse-index from StockUniverse.BySector
        private static readonly Dictionary<string, string> _symbolToSector =
            StockUniverse.BySector
                .SelectMany(kv => kv.Value.Select(sym => (sym, kv.Key)))
                .ToDictionary(t => t.sym, t => t.Key);

        public ScreenerService(IHistoricalDataService data)
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
                    string trendBucket = TrendBucket.For(bahReturn);

                    // ── Current RSI + slope (needs 14+ bars of close) ─────────
                    // RSI slope = today's RSI minus yesterday's RSI.
                    // Positive slope = RSI turning up (oversold bounce candidate).
                    // Negative slope = RSI still falling (too early to enter).
                    var closes = bars.Select(b => b.Close).ToList();
                    var rsiSeries = TechnicalIndicators.CalculateRsiSeries(closes, period: 14);
                    var validRsi = rsiSeries.Where(r => r.HasValue).Select(r => r!.Value).ToList();
                    decimal? currentRsi  = validRsi.Count >= 1 ? Math.Round(validRsi[^1], 1) : null;
                    decimal? previousRsi = validRsi.Count >= 2 ? Math.Round(validRsi[^2], 1) : null;
                    decimal? rsiSlope    = (currentRsi.HasValue && previousRsi.HasValue)
                                          ? Math.Round(currentRsi.Value - previousRsi.Value, 2)
                                          : null;

                    // ── Signal status (4-state pipeline) ──────────────────────
                    // 🔴 Entry Signal  — RSI < 30 AND slope up   → Track A (validated baseline)
                    // 🧪 Experimental  — RSI 30–40 AND slope up  → Track B (separate experiment)
                    // 🟡 Watching      — RSI < 40 AND slope down → monitor, no action
                    // ⚪ No Setup      — RSI >= 40               → nothing to do
                    string signalStatus = "No Setup";
                    if (currentRsi.HasValue && currentRsi < 40)
                    {
                        if (rsiSlope.HasValue && rsiSlope > 0)
                            signalStatus = currentRsi < 30 ? "Entry Signal" : "Experimental";
                        else
                            signalStatus = "Watching";
                    }

                    // ── Finding #1 exclusion rule ──────────────────────────────
                    bool passes = bahReturn <= 300;

                    var stock = new ScreenerStock
                    {
                        Symbol = symbol,
                        Sector = _symbolToSector.TryGetValue(symbol, out var sec) ? sec : "unknown",
                        BahReturn = bahReturn,
                        TrendBucket = trendBucket,
                        CurrentRsi = currentRsi,
                        PreviousRsi = previousRsi,
                        RsiSlope = rsiSlope,
                        SignalStatus = signalStatus,
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
                OversoldCount = candidates.Count(s => s.SignalStatus == "Entry Signal"),
                ExperimentalCount = candidates.Count(s => s.SignalStatus == "Experimental"),
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
        public int ExperimentalCount { get; set; }
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
        public decimal? PreviousRsi { get; set; }
        public decimal? RsiSlope { get; set; }
        public string SignalStatus { get; set; } = "No Setup";
        public bool Passes { get; set; }
        public string? ExcludeReason { get; set; }
    }
}