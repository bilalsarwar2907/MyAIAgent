using MyAIAgent.Services;
using MyAIAgent.Tools;

namespace MyAIAgent.Endpoints
{
    /// <summary>
    /// Strategy-comparison research: single symbol, per-sector aggregates, the
    /// full market sweep, and the trend-strength factor study (current + custom
    /// date range).
    ///
    /// TODO: the per-sector / all-sectors / factor bodies still do their
    /// aggregation (bucketing, medians, verdicts) inline with dynamic LINQ.
    /// That belongs in an IResearchQueryService so it can be unit-tested; left
    /// as a follow-up so the move here stays behaviour-preserving.
    /// </summary>
    public static class ResearchEndpoints
    {
        private static List<IStrategy> DefaultStrategies() => new()
        {
            new RsiStrategy(30, 70),
            new RsiStrategy(30, 70, trendFilter: true)
        };

        public static void MapResearchEndpoints(this WebApplication app)
        {
            app.MapGet("/research/{symbol}", async (string symbol, IResearchService researchService) =>
            {
                var report = await researchService.RunResearchAsync(symbol, DefaultStrategies());
                return Results.Text(researchService.FormatReport(report), "text/plain");
            });

            app.MapGet("/research/{symbol}/explain", async (string symbol, IAiService ai, IResearchService researchService, IEnumerable<ITool> tools) =>
            {
                var report = await researchService.RunResearchAsync(symbol, DefaultStrategies());

                if (!string.IsNullOrEmpty(report.Error))
                    return Results.Json(new { error = report.Error });

                var researchTool = tools.FirstOrDefault(t => t.Name == "ResearchStock") as StockResearchTool;
                var prompt = researchTool != null
                    ? await researchTool.ExecuteAsync(symbol)
                    : researchService.FormatForAI(report);

                var explanation = await ai.InterpretResearch(prompt);
                return Results.Json(new { symbol = symbol.ToUpper(), explanation });
            });

            app.MapGet("/research/batch/{sector}", async (string sector, IResearchService researchService) =>
            {
                if (!StockUniverse.BySector.TryGetValue(sector.ToLower(), out var symbols))
                    return Results.Text(
                        $"Unknown sector '{sector}'. Available: {string.Join(", ", StockUniverse.BySector.Keys)}",
                        "text/plain");

                var strategies = DefaultStrategies();

                var sb = new System.Text.StringBuilder();
                foreach (var symbol in symbols)
                {
                    var report = await researchService.RunResearchAsync(symbol, strategies);
                    sb.AppendLine(researchService.FormatReport(report));
                    sb.AppendLine(new string('─', 60));
                }

                return Results.Text(sb.ToString(), "text/plain");
            });

            // GET /research/sector/{sectorName} — per-sector aggregate for SectorResearchPanel.vue
            app.MapGet("/research/sector/{sectorName}", async (string sectorName, IResearchService researchService) =>
            {
                if (!StockUniverse.BySector.TryGetValue(sectorName.ToLower(), out var symbols))
                    return Results.Json(new { error = $"Unknown sector '{sectorName}'. Available: {string.Join(", ", StockUniverse.BySector.Keys)}" });

                var strategies = DefaultStrategies();

                var perSymbol = new List<object>();
                int beatCount = 0;
                var advantages = new List<decimal>();

                foreach (var symbol in symbols)
                {
                    var report = await researchService.RunResearchAsync(symbol, strategies);
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

                decimal medianAdvantage = MyAIAgent.Common.Stats.Median(advantages);

                return Results.Json(new
                {
                    sector = sectorName,
                    symbolsTested = perSymbol.Count,
                    beatCount,
                    medianAdvantage,
                    verdict = medianAdvantage >= 0 && beatCount >= perSymbol.Count / 2
                                        ? "Outperformed Benchmark"
                                        : "Underperformed Benchmark",
                    perSymbol
                });
            });

            // GET /research/all-sectors — full market overview (~3-4 min).
            app.MapGet("/research/all-sectors", async (IResearchService researchService) =>
            {
                var strategies = DefaultStrategies();

                var sectorSummaries = new List<object>();

                foreach (var (sector, symbols) in StockUniverse.BySector)
                {
                    int beatCount = 0;
                    var advantages = new List<decimal>();
                    string bestSymbol = "", worstSymbol = "";
                    decimal bestAdv = decimal.MinValue, worstAdv = decimal.MaxValue;

                    foreach (var symbol in symbols)
                    {
                        var report = await researchService.RunResearchAsync(symbol, strategies);
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

                    decimal median = MyAIAgent.Common.Stats.Median(advantages);

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

                return Results.Json(new
                {
                    sectorsRun = sectorSummaries.Count,
                    generatedAt = DateTime.UtcNow,
                    sectors = sectorSummaries.OrderByDescending(s => ((dynamic)s).medianAdvantage)
                });
            });

            // GET /research/factor/trend-strength — does trend strength predict RSI success?
            app.MapGet("/research/factor/trend-strength", async (IResearchService researchService) =>
            {
                var strategies = new List<IStrategy> { new RsiStrategy(30, 70) };

                var perStock = new List<object>();

                foreach (var symbol in StockUniverse.All)
                {
                    var report = await researchService.RunResearchAsync(symbol, strategies);
                    if (!string.IsNullOrEmpty(report.Error)) continue;

                    var baseline = report.Results.FirstOrDefault(r => r.Verdict == "Baseline");
                    var rsi = report.Results.FirstOrDefault(r => r.Verdict != "Baseline");
                    if (baseline == null || rsi == null) continue;

                    decimal bahReturn = baseline.TotalReturnPercent;
                    decimal rsiReturn = rsi.TotalReturnPercent;
                    decimal advantage = Math.Round(rsiReturn - bahReturn, 2);
                    bool beat = rsiReturn > bahReturn;

                    string trendBucket = MyAIAgent.Common.TrendBucket.For(bahReturn);

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

                var buckets = new[]
                {
                    MyAIAgent.Common.TrendBucket.Weak,
                    MyAIAgent.Common.TrendBucket.Medium,
                    MyAIAgent.Common.TrendBucket.Strong
                };
                var bucketStats = buckets.Select(bucket =>
                {
                    var stocks = perStock.Cast<dynamic>().Where(s => s.trendBucket == bucket).ToList();
                    if (!stocks.Any()) return null;

                    int total = stocks.Count;
                    int beatCount = stocks.Count(s => (bool)s.beat);
                    decimal median = MyAIAgent.Common.Stats.Median(
                        ((IEnumerable<dynamic>)stocks).Select(s => (decimal)s.advantage), round: 1);

                    return (object)new
                    {
                        bucket,
                        total,
                        beatCount,
                        beatRate = Math.Round((decimal)beatCount / total * 100, 1),
                        medianAdvantage = median
                    };
                }).Where(b => b != null).ToList();

                return Results.Json(new
                {
                    hypothesis = "RSI mean-reversion works better on weak-trend stocks than strong-trend stocks",
                    totalStocks = perStock.Count,
                    generatedAt = DateTime.UtcNow,
                    buckets = bucketStats,
                    perStock = perStock.Cast<dynamic>()
                                    .OrderBy(s => (decimal)s.bahReturn)
                                    .ToList()
                });
            });

            // GET /research/factor/trend-strength/{fromYear}/{toYear} — same, custom period.
            app.MapGet("/research/factor/trend-strength/{fromYear}/{toYear}",
                async (int fromYear, int toYear, IHistoricalDataService historicalData) =>
                {
                    var from = new DateTime(fromYear, 1, 1);
                    var to = new DateTime(toYear, 12, 31);

                    var perStock = new List<object>();

                    foreach (var symbol in StockUniverse.All)
                    {
                        List<DailyBar> bars;
                        try { bars = await historicalData.GetDailyHistoryRangeAsync(symbol, from, to); }
                        catch { continue; }

                        if (bars.Count < 60) continue;

                        var firstPrice = bars.First().Close;
                        var lastPrice = bars.Last().Close;
                        decimal bahReturn = Math.Round(((lastPrice - firstPrice) / firstPrice) * 100, 2);

                        var rsiStrategy = new RsiStrategy(30, 70);
                        var trades = rsiStrategy.Run(bars);

                        decimal rsiReturn = MyAIAgent.Common.EquityCurve
                            .Compound(trades.Select(t => t.ReturnPercent)).TotalReturnPercent;
                        decimal advantage = Math.Round(rsiReturn - bahReturn, 2);
                        bool beat = rsiReturn > bahReturn;

                        string trendBucket = MyAIAgent.Common.TrendBucket.For(bahReturn);

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

                    var buckets = new[]
                    {
                        MyAIAgent.Common.TrendBucket.Weak,
                        MyAIAgent.Common.TrendBucket.Medium,
                        MyAIAgent.Common.TrendBucket.Strong
                    };
                    var bucketStats = buckets.Select(bucket =>
                    {
                        var stocks = perStock.Cast<dynamic>().Where(s => s.trendBucket == bucket).ToList();
                        if (!stocks.Any()) return null;

                        int total = stocks.Count;
                        int beatCount = stocks.Count(s => (bool)s.beat);
                        decimal median = MyAIAgent.Common.Stats.Median(
                            ((IEnumerable<dynamic>)stocks).Select(s => (decimal)s.advantage), round: 1);

                        return (object)new
                        {
                            bucket,
                            total,
                            beatCount,
                            beatRate = Math.Round((decimal)beatCount / total * 100, 1),
                            medianAdvantage = median
                        };
                    }).Where(b => b != null).ToList();

                    return Results.Json(new
                    {
                        hypothesis = "RSI mean-reversion works better on weak-trend stocks than strong-trend stocks",
                        period = $"{fromYear}–{toYear}",
                        totalStocks = perStock.Count,
                        generatedAt = DateTime.UtcNow,
                        buckets = bucketStats,
                        perStock = perStock.Cast<dynamic>()
                                        .OrderBy(s => (decimal)s.bahReturn)
                                        .ToList()
                    });
                });
        }
    }
}
