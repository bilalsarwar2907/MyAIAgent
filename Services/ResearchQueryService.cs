using MyAIAgent.Common;

namespace MyAIAgent.Services
{
    /// <inheritdoc />
    public class ResearchQueryService : IResearchQueryService
    {
        private readonly IResearchService _research;
        private readonly IHistoricalDataService _data;

        public ResearchQueryService(IResearchService research, IHistoricalDataService data)
        {
            _research = research;
            _data = data;
        }

        private static List<IStrategy> TwoStrategies() => new()
        {
            new RsiStrategy(30, 70),
            new RsiStrategy(30, 70, trendFilter: true)
        };

        public async Task<object> SectorSummaryAsync(string sectorName)
        {
            if (!StockUniverse.BySector.TryGetValue(sectorName.ToLower(), out var symbols))
                return new { error = $"Unknown sector '{sectorName}'. Available: {string.Join(", ", StockUniverse.BySector.Keys)}" };

            var strategies = TwoStrategies();

            var perSymbol = new List<object>();
            int beatCount = 0;
            var advantages = new List<decimal>();

            foreach (var symbol in symbols)
            {
                var report = await _research.RunResearchAsync(symbol, strategies);
                if (!string.IsNullOrEmpty(report.Error)) continue;

                var baseline = report.Results.FirstOrDefault(r => r.Verdict == "Baseline");
                var best = report.Results
                    .Where(r => r.Verdict != "Baseline")
                    .OrderByDescending(r => r.TotalReturnPercent)
                    .FirstOrDefault();

                if (baseline == null || best == null) continue;

                decimal advantage = Math.Round(best.TotalReturnPercent - baseline.TotalReturnPercent, 2);
                bool beat = best.TotalReturnPercent > baseline.TotalReturnPercent;
                if (beat) beatCount++;
                advantages.Add(advantage);

                perSymbol.Add(new
                {
                    symbol,
                    bahReturn = Math.Round(baseline.TotalReturnPercent, 2),
                    bestStrategy = best.StrategyName,
                    stratReturn = Math.Round(best.TotalReturnPercent, 2),
                    advantage,
                    beat,
                    trades = best.TotalTrades,
                    winRate = best.WinRate,
                    maxDrawdown = best.MaxDrawdownPercent
                });
            }

            decimal medianAdvantage = Stats.Median(advantages);

            return new
            {
                sector = sectorName,
                symbolsTested = perSymbol.Count,
                beatCount,
                medianAdvantage,
                verdict = medianAdvantage >= 0 && beatCount >= perSymbol.Count / 2
                                    ? "Outperformed Benchmark"
                                    : "Underperformed Benchmark",
                perSymbol
            };
        }

        public async Task<object> AllSectorsAsync()
        {
            var strategies = TwoStrategies();

            var sectorSummaries = new List<object>();

            foreach (var (sector, symbols) in StockUniverse.BySector)
            {
                int beatCount = 0;
                var advantages = new List<decimal>();
                string bestSymbol = "", worstSymbol = "";
                decimal bestAdv = decimal.MinValue, worstAdv = decimal.MaxValue;

                foreach (var symbol in symbols)
                {
                    var report = await _research.RunResearchAsync(symbol, strategies);
                    if (!string.IsNullOrEmpty(report.Error)) continue;

                    var baseline = report.Results.FirstOrDefault(r => r.Verdict == "Baseline");
                    var best = report.Results
                        .Where(r => r.Verdict != "Baseline")
                        .OrderByDescending(r => r.TotalReturnPercent)
                        .FirstOrDefault();

                    if (baseline == null || best == null) continue;

                    decimal adv = Math.Round(best.TotalReturnPercent - baseline.TotalReturnPercent, 2);
                    if (best.TotalReturnPercent > baseline.TotalReturnPercent) beatCount++;
                    advantages.Add(adv);

                    if (adv > bestAdv) { bestAdv = adv; bestSymbol = symbol; }
                    if (adv < worstAdv) { worstAdv = adv; worstSymbol = symbol; }
                }

                decimal median = Stats.Median(advantages);

                sectorSummaries.Add(new
                {
                    sector,
                    symbolsTested = advantages.Count,
                    beatCount,
                    medianAdvantage = median,
                    verdict = median >= 0 && beatCount >= advantages.Count / 2
                                        ? "Outperformed"
                                        : "Underperformed",
                    bestSymbol,
                    bestAdvantage = bestAdv == decimal.MinValue ? 0 : bestAdv,
                    worstSymbol,
                    worstAdvantage = worstAdv == decimal.MaxValue ? 0 : worstAdv
                });
            }

            return new
            {
                sectorsRun = sectorSummaries.Count,
                generatedAt = DateTime.UtcNow,
                sectors = sectorSummaries.OrderByDescending(s => ((dynamic)s).medianAdvantage)
            };
        }

        public async Task<object> TrendStrengthFactorAsync()
        {
            var strategies = new List<IStrategy> { new RsiStrategy(30, 70) };

            var perStock = new List<object>();

            foreach (var symbol in StockUniverse.All)
            {
                var report = await _research.RunResearchAsync(symbol, strategies);
                if (!string.IsNullOrEmpty(report.Error)) continue;

                var baseline = report.Results.FirstOrDefault(r => r.Verdict == "Baseline");
                var rsi = report.Results.FirstOrDefault(r => r.Verdict != "Baseline");
                if (baseline == null || rsi == null) continue;

                decimal bahReturn = baseline.TotalReturnPercent;
                decimal rsiReturn = rsi.TotalReturnPercent;
                decimal advantage = Math.Round(rsiReturn - bahReturn, 2);
                bool beat = rsiReturn > bahReturn;

                string trendBucket = TrendBucket.For(bahReturn);

                perStock.Add(new
                {
                    symbol,
                    bahReturn = Math.Round(bahReturn, 1),
                    rsiReturn = Math.Round(rsiReturn, 1),
                    advantage,
                    beat,
                    trendBucket,
                    trades = rsi.TotalTrades,
                    winRate = rsi.WinRate
                });
            }

            return new
            {
                hypothesis = "RSI mean-reversion works better on weak-trend stocks than strong-trend stocks",
                totalStocks = perStock.Count,
                generatedAt = DateTime.UtcNow,
                buckets = BucketStats(perStock),
                perStock = perStock.Cast<dynamic>()
                                .OrderBy(s => (decimal)s.bahReturn)
                                .ToList()
            };
        }

        public async Task<object> TrendStrengthFactorRangeAsync(int fromYear, int toYear)
        {
            var from = new DateTime(fromYear, 1, 1);
            var to = new DateTime(toYear, 12, 31);

            var perStock = new List<object>();

            foreach (var symbol in StockUniverse.All)
            {
                List<DailyBar> bars;
                try { bars = await _data.GetDailyHistoryRangeAsync(symbol, from, to); }
                catch { continue; }

                if (bars.Count < 60) continue;

                var firstPrice = bars.First().Close;
                var lastPrice = bars.Last().Close;
                decimal bahReturn = Math.Round(((lastPrice - firstPrice) / firstPrice) * 100, 2);

                var trades = new RsiStrategy(30, 70).Run(bars);

                decimal rsiReturn = EquityCurve.Compound(trades.Select(t => t.ReturnPercent)).TotalReturnPercent;
                decimal advantage = Math.Round(rsiReturn - bahReturn, 2);
                bool beat = rsiReturn > bahReturn;

                string trendBucket = TrendBucket.For(bahReturn);

                perStock.Add(new
                {
                    symbol,
                    bahReturn,
                    rsiReturn,
                    advantage,
                    beat,
                    trendBucket,
                    trades = trades.Count,
                    winRate = trades.Count > 0
                        ? Math.Round((decimal)trades.Count(t => t.ReturnPercent > 0) / trades.Count * 100, 1)
                        : 0
                });
            }

            return new
            {
                hypothesis = "RSI mean-reversion works better on weak-trend stocks than strong-trend stocks",
                period = $"{fromYear}–{toYear}",
                totalStocks = perStock.Count,
                generatedAt = DateTime.UtcNow,
                buckets = BucketStats(perStock),
                perStock = perStock.Cast<dynamic>()
                                .OrderBy(s => (decimal)s.bahReturn)
                                .ToList()
            };
        }

        // Shared bucket rollup for both trend-strength endpoints.
        private static List<object> BucketStats(List<object> perStock)
        {
            var buckets = new[] { TrendBucket.Weak, TrendBucket.Medium, TrendBucket.Strong };

            return buckets.Select(bucket =>
            {
                var stocks = perStock.Cast<dynamic>().Where(s => s.trendBucket == bucket).ToList();
                if (!stocks.Any()) return null;

                int total = stocks.Count;
                int beatCount = stocks.Count(s => (bool)s.beat);
                decimal median = Stats.Median(
                    ((IEnumerable<dynamic>)stocks).Select(s => (decimal)s.advantage), round: 1);

                return (object)new
                {
                    bucket,
                    total,
                    beatCount,
                    beatRate = Math.Round((decimal)beatCount / total * 100, 1),
                    medianAdvantage = median
                };
            }).Where(b => b != null).ToList()!;
        }
    }
}
