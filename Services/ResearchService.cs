namespace MyAIAgent.Services
{
    public class StrategyResult
    {
        public string StrategyName { get; set; } = "";
        public string StrategyDescription { get; set; } = "";
        public int TotalTrades { get; set; }
        public decimal WinRate { get; set; }
        public decimal TotalReturnPercent { get; set; }
        public decimal MaxDrawdownPercent { get; set; }
        public string Verdict { get; set; } = "";   // "Beat buy-and-hold" | "Underperformed buy-and-hold" | "Baseline"
    }

    public class SymbolResearchReport
    {
        public string Symbol { get; set; } = "";
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public int TradingDaysInPeriod { get; set; }
        public List<StrategyResult> Results { get; set; } = new();
        public string Error { get; set; } = "";
    }

    /// <summary>
    /// Runs one or more strategies against the same historical data for a symbol
    /// and returns a structured, side-by-side comparison.
    /// This is the core of the "evidence-based research platform" — the output
    /// goes either to the REST endpoint (plain text) or to the AI interpretation
    /// layer (StockResearchTool) for a human-readable explanation.
    /// </summary>
    public class ResearchService : IResearchService
    {
        private readonly IHistoricalDataService _data;

        public ResearchService(IHistoricalDataService data)
        {
            _data = data;
        }

        public async Task<SymbolResearchReport> RunResearchAsync(
            string symbol,
            IEnumerable<IStrategy> strategies)
        {
            var report = new SymbolResearchReport { Symbol = symbol.ToUpper() };

            List<DailyBar> bars;
            try { bars = await _data.GetDailyHistoryAsync(symbol); }
            catch (Exception ex) { report.Error = ex.Message; return report; }

            if (bars.Count < 60)
            {
                report.Error = $"Not enough data for {symbol} ({bars.Count} days).";
                return report;
            }

            report.PeriodStart = bars.First().Date;
            report.PeriodEnd = bars.Last().Date;
            report.TradingDaysInPeriod = bars.Count;

            // Run buy-and-hold first so we have the baseline return for verdicts.
            // Drawdown is computed daily (mark-to-market from the entry price),
            // not on closed trades -- buy-and-hold never closes, so a closed-trade
            // drawdown would always be 0, which is misleading.
            var bah = new BuyAndHoldStrategy();
            var bahTrades = bah.Run(bars);
            decimal bahReturn = bahTrades.Count > 0 ? bahTrades[0].ReturnPercent : 0;

            decimal bahPeak = bars.First().Close;
            decimal bahWorstDD = 0;
            foreach (var bar in bars)
            {
                if (bar.Close > bahPeak) bahPeak = bar.Close;
                var dd = (bar.Close - bahPeak) / bahPeak * 100;
                if (dd < bahWorstDD) bahWorstDD = dd;
            }

            report.Results.Add(new StrategyResult
            {
                StrategyName = bah.Name,
                StrategyDescription = bah.Description,
                TotalTrades = 1,
                WinRate = bahReturn > 0 ? 100 : 0,
                TotalReturnPercent = Math.Round(bahReturn, 2),
                MaxDrawdownPercent = Math.Round(bahWorstDD, 2),
                Verdict = "Baseline"
            });

            // Run each additional strategy and compare against buy-and-hold
            foreach (var strategy in strategies)
            {
                var trades = strategy.Run(bars);

                decimal equity = 1, peak = 1, worstDD = 0;
                foreach (var t in trades)
                {
                    equity *= (1 + t.ReturnPercent / 100);
                    if (equity > peak) peak = equity;
                    var dd = (equity - peak) / peak * 100;
                    if (dd < worstDD) worstDD = dd;
                }
                decimal totalReturn = Math.Round((equity - 1) * 100, 2);
                decimal winRate = trades.Count > 0
                    ? Math.Round((decimal)trades.Count(t => t.ReturnPercent > 0) / trades.Count * 100, 1)
                    : 0;

                report.Results.Add(new StrategyResult
                {
                    StrategyName = strategy.Name,
                    StrategyDescription = strategy.Description,
                    TotalTrades = trades.Count,
                    WinRate = winRate,
                    TotalReturnPercent = totalReturn,
                    MaxDrawdownPercent = Math.Round(worstDD, 2),
                    Verdict = trades.Count == 0
                        ? "No trades triggered"
                        : totalReturn > bahReturn
                            ? "Beat buy-and-hold"
                            : "Underperformed buy-and-hold"
                });
            }

            return report;
        }

        /// <summary>
        /// Plain-text report for the REST endpoint — readable in browser or Postman.
        /// </summary>
        public string FormatReport(SymbolResearchReport report)
        {
            if (!string.IsNullOrEmpty(report.Error))
                return $"❌ {report.Symbol}: {report.Error}";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"=== RESEARCH REPORT: {report.Symbol} ===");
            sb.AppendLine($"Period: {report.PeriodStart:yyyy-MM-dd} → {report.PeriodEnd:yyyy-MM-dd} ({report.TradingDaysInPeriod} trading days)");
            sb.AppendLine();

            foreach (var r in report.Results)
            {
                sb.AppendLine($"📊 {r.StrategyName}");
                sb.AppendLine($"   Rule:     {r.StrategyDescription}");
                if (r.Verdict != "Baseline")
                {
                    sb.AppendLine($"   Trades:   {r.TotalTrades}  |  Win rate: {r.WinRate}%");
                    sb.AppendLine($"   Return:   {r.TotalReturnPercent}%  |  Max drawdown: {r.MaxDrawdownPercent}%");
                }
                else
                {
                    sb.AppendLine($"   Return:   {r.TotalReturnPercent}%");
                }
                sb.AppendLine($"   Verdict:  {r.Verdict}");
                sb.AppendLine();
            }

            sb.AppendLine("⚠️ Historical simulation only — past performance does not predict future results.");
            return sb.ToString();
        }

        /// <summary>
        /// Compact structured summary fed to the AI interpretation tool.
        /// Keeps the prompt short so it doesn't eat into the Ollama context window.
        /// </summary>
        public string FormatForAI(SymbolResearchReport report)
        {
            if (!string.IsNullOrEmpty(report.Error))
                return $"Research failed for {report.Symbol}: {report.Error}";

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"Symbol: {report.Symbol}");
            sb.AppendLine($"Period: {report.PeriodStart:yyyy-MM-dd} to {report.PeriodEnd:yyyy-MM-dd} ({report.TradingDaysInPeriod} trading days)");
            sb.AppendLine();

            foreach (var r in report.Results)
            {
                sb.AppendLine($"Strategy: {r.StrategyName}");
                sb.AppendLine($"Rule: {r.StrategyDescription}");
                if (r.Verdict != "Baseline")
                    sb.AppendLine($"Trades: {r.TotalTrades} | Win rate: {r.WinRate}% | Return: {r.TotalReturnPercent}% | Max drawdown: {r.MaxDrawdownPercent}% | Verdict: {r.Verdict}");
                else
                    sb.AppendLine($"Return: {r.TotalReturnPercent}% | Verdict: Baseline");
                sb.AppendLine();
            }

            return sb.ToString();
        }
    }
}