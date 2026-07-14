using MyAIAgent.Services;
using System.Text.Json;

namespace MyAIAgent.Tools
{
    public class StockResearchTool : ITool
    {
        public string Name => "ResearchStock";

        private readonly ResearchService _research;

        public StockResearchTool(ResearchService research)
        {
            _research = research;
        }

        public string Execute(string input)
        {
            return RunAsync(input.Trim().ToUpper()).GetAwaiter().GetResult();
        }

        private async Task<string> RunAsync(string symbol)
        {
            var strategies = new List<IStrategy>
            {
                new RsiStrategy(30, 70),
                new RsiStrategy(30, 70, trendFilter: true)
            };

            var report = await _research.RunResearchAsync(symbol, strategies);

            if (!string.IsNullOrEmpty(report.Error))
                return $"Could not run research for {symbol}: {report.Error}";

            return BuildPrompt(symbol, report);
        }

        private string BuildPrompt(string symbol, SymbolResearchReport report)
        {
            var baseline = report.Results.FirstOrDefault(r => r.Verdict == "Baseline");
            decimal bahReturn = baseline?.TotalReturnPercent ?? 0;
            int years = (int)Math.Round((report.PeriodEnd - report.PeriodStart).TotalDays / 365.25);

            var strategies = report.Results
                .Where(r => r.Verdict != "Baseline")
                .Select(r =>
                {
                    bool profitable = r.TotalReturnPercent > 0;
                    bool beatBuyHold = r.TotalReturnPercent > bahReturn;
                    decimal gap = Math.Round(r.TotalReturnPercent - bahReturn, 2);

                    return new
                    {
                        name = r.StrategyName,
                        rule = r.StrategyDescription,
                        trades = r.TotalTrades,
                        winRatePct = r.WinRate,
                        strategyReturnPct = r.TotalReturnPercent,
                        maxDrawdownPct = r.MaxDrawdownPercent,
                        isProfitable = profitable,
                        beatBuyAndHold = beatBuyHold,
                        gapVsBuyHoldPct = gap,
                        verdict = r.TotalTrades == 0
                                              ? "No trades triggered"
                                              : beatBuyHold
                                                  ? $"Outperformed buy-and-hold by {Math.Abs(gap)}%"
                                                  : $"Underperformed buy-and-hold by {Math.Abs(gap)}%"
                    };
                })
                .ToList();

            var payload = new
            {
                ticker = symbol,
                periodYears = years,
                periodStart = report.PeriodStart.ToString("yyyy-MM-dd"),
                periodEnd = report.PeriodEnd.ToString("yyyy-MM-dd"),
                buyAndHoldReturnPct = bahReturn,
                strategies
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                WriteIndented = true
            });

            return $"""
=== RESEARCH DATA (JSON) ===
{json}
=== END DATA ===

INSTRUCTIONS:
The numbers above are already visible to the user on screen.
Do NOT repeat them as a summary.

Instead, write 3-4 sentences that explain the IMPLICATIONS of what the data shows.

Write only what is directly supported by the data. Do not speculate.
Banned words: "could be", "might", "possibly", "perhaps", "may suggest", "potentially".

Respond in this exact format — no numbered lists, no paragraphs, no preamble:

Why it worked / failed
• [one sentence on price behavior — e.g. captured large cyclical swings vs fought a strong uptrend]
• [one sentence on what the trade count reveals — e.g. few trades = strategy waited for large moves]

Risk
• [one sentence on what the drawdown means in practice — e.g. investors held through a X% loss before recovering]
• [one honest limitation — e.g. X trades over Y years is a small sample]

End with exactly one line: "Past performance does not predict future results."

Rules:
- Do not repeat return %, win rate, or trade count — those are already on screen
- Do not say "buy", "sell", or make any prediction
- Do not contradict the pre-computed verdicts in the JSON
- Maximum 5 bullet points total across both sections
""";
        }
    }
}