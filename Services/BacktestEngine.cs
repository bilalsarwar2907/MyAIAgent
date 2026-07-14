// ═══ LOCKED BASE: BacktestEngine ═══
// Verified correct as of June 2026 audit. All 59-stock results in the
// First Report were produced by this engine.
//
// Key design decisions (do not change without re-validating results):
// - Uses TechnicalIndicators.CalculateRsiSeries (fixed, nullable series)
// - null RSI entries are skipped -- never defaulted to 0
// - Drawdown tracks closed-trade equity curve only (not daily MTM);
//   this is a known simplification, documented in the class summary
// - Trend filter blocks entry when price > 200-day SMA
// - Open positions at end of data are discarded (no partial-trade credit)
//
// Safe to build on: RunSingleAsync, RunBatchAsync, FormatReport
// ═════════════════════════════
namespace MyAIAgent.Services
{
    public class SimulatedTrade
    {
        public DateTime BuyDate { get; set; }
        public decimal BuyPrice { get; set; }
        public DateTime SellDate { get; set; }
        public decimal SellPrice { get; set; }
        public decimal ReturnPercent { get; set; }
    }

    public class SymbolBacktestResult
    {
        public string Symbol { get; set; } = "";
        public int TotalTrades { get; set; }
        public int WinningTrades { get; set; }
        public int LosingTrades { get; set; }
        public decimal WinRate { get; set; }
        public decimal TotalReturnPercent { get; set; }       // compounded across all trades
        public decimal BuyAndHoldReturnPercent { get; set; }
        public decimal MaxDrawdownPercent { get; set; }       // worst peak-to-trough on this symbol's TRADE equity curve (see note below)
        public List<SimulatedTrade> Trades { get; set; } = new();
        public string Error { get; set; } = "";
    }

    public class BacktestSummary
    {
        public string StrategyDescription { get; set; } = "";
        public int SymbolsTested { get; set; }
        public int SymbolsWithErrors { get; set; }
        public int TotalTrades { get; set; }
        public decimal AverageWinRate { get; set; }
        public decimal AverageReturnPercent { get; set; }
        public decimal AverageBuyAndHoldReturnPercent { get; set; }
        public int SymbolsThatBeatBuyAndHold { get; set; }
        public decimal WorstDrawdownPercent { get; set; }     // worst single-symbol drawdown across the whole batch
        public List<SymbolBacktestResult> PerSymbolResults { get; set; } = new();
    }

    /// <summary>
    /// Same strategy as BacktestTool — buy when RSI(14) &lt; 30, sell when RSI(14) &gt; 70 —
    /// run against Stooq's free multi-year history instead of Alpha Vantage's
    /// 25/day, ~100-day compact data, across many symbols in one pass.
    ///
    /// NOTE on MaxDrawdown: this tracks drawdown on the equity curve formed by
    /// CLOSED trades only (equity only changes when a trade exits), not a daily
    /// mark-to-market curve. It will under-state the worst-case drawdown you'd
    /// actually experience while holding a losing position that hasn't sold yet.
    /// That's a known simplification — a full daily mark-to-market drawdown is a
    /// reasonable later upgrade once the core go/no-go question is answered.
    /// </summary>
    public class BacktestEngine
    {
        private readonly HistoricalDataService _historicalData;

        public BacktestEngine(HistoricalDataService historicalData)
        {
            _historicalData = historicalData;
        }

        /// <summary>
        /// Runs the strategy across every symbol in the list and aggregates results.
        /// Symbols that fail to fetch (bad ticker, Stooq hiccup) show up with an
        /// Error in their per-symbol result and are excluded from the averages,
        /// not silently dropped.
        /// </summary>
        public async Task<BacktestSummary> RunBatchAsync(
            IEnumerable<string> symbols,
            int rsiBuyThreshold = 30,
            int rsiSellThreshold = 70,
            bool useTrendFilter = false)
        {
            var summary = new BacktestSummary
            {
                StrategyDescription =
                    $"Buy when RSI(14) < {rsiBuyThreshold} (oversold), sell when RSI(14) > {rsiSellThreshold} (overbought)"
            };

            foreach (var symbol in symbols)
            {
                var result = await RunSingleAsync(symbol, rsiBuyThreshold, rsiSellThreshold, useTrendFilter);
                summary.PerSymbolResults.Add(result);

                if (!string.IsNullOrEmpty(result.Error))
                {
                    summary.SymbolsWithErrors++;
                    continue;
                }

                summary.SymbolsTested++;
                summary.TotalTrades += result.TotalTrades;
                if (result.TotalReturnPercent > result.BuyAndHoldReturnPercent)
                    summary.SymbolsThatBeatBuyAndHold++;
                if (result.MaxDrawdownPercent < summary.WorstDrawdownPercent)
                    summary.WorstDrawdownPercent = result.MaxDrawdownPercent;
            }

            var valid = summary.PerSymbolResults.Where(r => string.IsNullOrEmpty(r.Error)).ToList();
            if (valid.Count > 0)
            {
                summary.AverageWinRate = Math.Round(valid.Average(r => r.WinRate), 2);
                summary.AverageReturnPercent = Math.Round(valid.Average(r => r.TotalReturnPercent), 2);
                summary.AverageBuyAndHoldReturnPercent = Math.Round(valid.Average(r => r.BuyAndHoldReturnPercent), 2);
            }

            return summary;
        }
        /// <summary>
        /// Multi-period validation overload.
        /// Same logic as RunBatchAsync but fetches bars between fromDate and toDate
        /// instead of the default rolling 10y window.
        /// Used for 2006–2016 validation run — does not affect locked First Report results.
        /// </summary>
        public async Task<BacktestSummary> RunBatchRangeAsync(
            IEnumerable<string> symbols,
            DateTime fromDate,
            DateTime toDate,
            int rsiBuyThreshold = 30,
            int rsiSellThreshold = 70,
            bool useTrendFilter = false)
        {
            var summary = new BacktestSummary
            {
                StrategyDescription =
                    $"Buy RSI(14)<{rsiBuyThreshold} / Sell RSI(14)>{rsiSellThreshold}" +
                    $" | Period: {fromDate:yyyy-MM-dd} to {toDate:yyyy-MM-dd}"
            };

            foreach (var symbol in symbols)
            {
                var result = await RunSingleRangeAsync(
                    symbol, fromDate, toDate,
                    rsiBuyThreshold, rsiSellThreshold, useTrendFilter);

                summary.PerSymbolResults.Add(result);

                if (!string.IsNullOrEmpty(result.Error))
                {
                    summary.SymbolsWithErrors++;
                    continue;
                }

                summary.SymbolsTested++;
                summary.TotalTrades += result.TotalTrades;
                if (result.TotalReturnPercent > result.BuyAndHoldReturnPercent)
                    summary.SymbolsThatBeatBuyAndHold++;
                if (result.MaxDrawdownPercent < summary.WorstDrawdownPercent)
                    summary.WorstDrawdownPercent = result.MaxDrawdownPercent;
            }

            var valid = summary.PerSymbolResults.Where(r => string.IsNullOrEmpty(r.Error)).ToList();
            if (valid.Count > 0)
            {
                summary.AverageWinRate = Math.Round(valid.Average(r => r.WinRate), 2);
                summary.AverageReturnPercent = Math.Round(valid.Average(r => r.TotalReturnPercent), 2);
                summary.AverageBuyAndHoldReturnPercent = Math.Round(valid.Average(r => r.BuyAndHoldReturnPercent), 2);
            }

            return summary;
        }

        /// <summary>
        /// Single-symbol range backtest. Same logic as RunSingleAsync
        /// but calls GetDailyHistoryRangeAsync instead of GetDailyHistoryAsync.
        /// </summary>
        public async Task<SymbolBacktestResult> RunSingleRangeAsync(
            string symbol,
            DateTime fromDate,
            DateTime toDate,
            int rsiBuyThreshold = 30,
            int rsiSellThreshold = 70,
            bool useTrendFilter = false)
        {
            var result = new SymbolBacktestResult { Symbol = symbol.ToUpper() };

            List<DailyBar> bars;
            try
            {
                bars = await _historicalData.GetDailyHistoryRangeAsync(symbol, fromDate, toDate);
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                return result;
            }

            if (bars.Count < 60)
            {
                result.Error = $"Not enough data for {symbol} in range ({bars.Count} days).";
                return result;
            }

            var closes = bars.Select(b => b.Close).ToList();
            var rsiSeries = TechnicalIndicators.CalculateRsiSeries(closes, 14);

            List<decimal?> sma200Series = useTrendFilter
                ? TechnicalIndicators.CalculateSmaSeries(closes, 200)
                : null;

            var trades = new List<SimulatedTrade>();
            bool holding = false;
            DateTime buyDate = default;
            decimal buyPrice = 0;

            for (int i = 0; i < bars.Count; i++)
            {
                var rsi = rsiSeries[i];
                if (rsi == null) continue;

                bool trendAllowsEntry = true;
                if (useTrendFilter && sma200Series != null)
                {
                    var sma200 = sma200Series[i];
                    trendAllowsEntry = sma200 != null ? bars[i].Close < sma200 : false;
                }

                if (!holding && rsi < rsiBuyThreshold && trendAllowsEntry)
                {
                    holding = true;
                    buyDate = bars[i].Date;
                    buyPrice = bars[i].Close;
                }
                else if (holding && rsi > rsiSellThreshold)
                {
                    var returnPct = ((bars[i].Close - buyPrice) / buyPrice) * 100;
                    trades.Add(new SimulatedTrade
                    {
                        BuyDate = buyDate,
                        BuyPrice = buyPrice,
                        SellDate = bars[i].Date,
                        SellPrice = bars[i].Close,
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

            decimal equity = 1, peak = 1, worstDrawdown = 0;
            foreach (var t in trades)
            {
                equity *= (1 + (t.ReturnPercent / 100));
                if (equity > peak) peak = equity;
                var dd = (equity - peak) / peak * 100;
                if (dd < worstDrawdown) worstDrawdown = dd;
            }
            result.TotalReturnPercent = Math.Round((equity - 1) * 100, 2);
            result.MaxDrawdownPercent = Math.Round(worstDrawdown, 2);
            result.BuyAndHoldReturnPercent = Math.Round(
                ((bars.Last().Close - bars.First().Close) / bars.First().Close) * 100, 2);

            return result;
        }

        public async Task<SymbolBacktestResult> RunSingleAsync(
            string symbol,
            int rsiBuyThreshold = 30,
            int rsiSellThreshold = 70,
            bool useTrendFilter = false)
        {
            var result = new SymbolBacktestResult { Symbol = symbol.ToUpper() };

            List<DailyBar> bars;
            try
            {
                bars = await _historicalData.GetDailyHistoryAsync(symbol);
            }
            catch (Exception ex)
            {
                result.Error = ex.Message;
                return result;
            }

            if (bars.Count < 60)
            {
                result.Error = $"Not enough historical data for {symbol} ({bars.Count} days returned).";
                return result;
            }

            var closes = bars.Select(b => b.Close).ToList();
            var rsiSeries = TechnicalIndicators.CalculateRsiSeries(closes, 14);

            // Trend filter: 200-day SMA computed once for the whole series.
            // Entry is only allowed when price is BELOW the 200-day MA — i.e.
            // the stock is not in a strong uptrend, so mean-reversion is plausible.
            // When price is above the 200-day MA the stock is likely trending up;
            // RSI<30 dips in that environment are usually brief and quickly recover
            // far beyond our RSI>70 exit, so we stay out rather than chasing.
            List<decimal?> sma200Series = useTrendFilter
                ? TechnicalIndicators.CalculateSmaSeries(closes, 200)
                : null;

            var trades = new List<SimulatedTrade>();
            bool holding = false;
            DateTime buyDate = default;
            decimal buyPrice = 0;

            for (int i = 0; i < bars.Count; i++)
            {
                var rsi = rsiSeries[i];
                if (rsi == null) continue; // not enough history yet — skip, don't default to 0

                // Trend filter guard: skip entry if price is above 200-day MA.
                // Once we're already holding we don't force an early exit — we
                // let the normal RSI>70 exit rule handle it.
                bool trendAllowsEntry = true;
                if (useTrendFilter && sma200Series != null)
                {
                    var sma200 = sma200Series[i];
                    if (sma200 != null)
                        trendAllowsEntry = bars[i].Close < sma200;
                    else
                        trendAllowsEntry = false; // not enough history for 200-day yet
                }

                if (!holding && rsi < rsiBuyThreshold && trendAllowsEntry)
                {
                    holding = true;
                    buyDate = bars[i].Date;
                    buyPrice = bars[i].Close;
                }
                else if (holding && rsi > rsiSellThreshold)
                {
                    var sellDate = bars[i].Date;
                    var sellPrice = bars[i].Close;
                    var returnPct = ((sellPrice - buyPrice) / buyPrice) * 100;

                    trades.Add(new SimulatedTrade
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

            // Compounded return + drawdown on the closed-trade equity curve
            decimal equity = 1;
            decimal peak = 1;
            decimal worstDrawdown = 0;
            foreach (var t in trades)
            {
                equity *= (1 + (t.ReturnPercent / 100));
                if (equity > peak) peak = equity;
                var drawdown = (equity - peak) / peak * 100;
                if (drawdown < worstDrawdown) worstDrawdown = drawdown;
            }
            result.TotalReturnPercent = Math.Round((equity - 1) * 100, 2);
            result.MaxDrawdownPercent = Math.Round(worstDrawdown, 2);

            var firstPrice = bars.First().Close;
            var lastPrice = bars.Last().Close;
            result.BuyAndHoldReturnPercent = Math.Round(((lastPrice - firstPrice) / firstPrice) * 100, 2);

            return result;
        }

        /// <summary>
        /// Plain-text report in the same style as BacktestTool's existing output,
        /// so it's easy to read straight from a browser/Postman hit on the endpoint.
        /// </summary>
        public string FormatReport(BacktestSummary summary)
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("=== LARGE-SCALE BACKTEST ===");
            sb.AppendLine("Strategy: " + summary.StrategyDescription);
            sb.AppendLine("Data source: Yahoo Finance (full daily history, no 25/day cap)");
            sb.AppendLine();

            foreach (var r in summary.PerSymbolResults)
            {
                if (!string.IsNullOrEmpty(r.Error))
                {
                    sb.AppendLine("❌ " + r.Symbol + ": " + r.Error);
                    continue;
                }

                var outcome = r.TotalReturnPercent > r.BuyAndHoldReturnPercent ? "BEAT buy-and-hold" : "underperformed buy-and-hold";
                sb.AppendLine("📊 " + r.Symbol +
                    " | Trades: " + r.TotalTrades +
                    " | Win rate: " + r.WinRate + "%" +
                    " | Strategy: " + r.TotalReturnPercent + "%" +
                    " | Buy&Hold: " + r.BuyAndHoldReturnPercent + "%" +
                    " | Max DD: " + r.MaxDrawdownPercent + "%" +
                    " | " + outcome);
            }

            sb.AppendLine();
            sb.AppendLine("=== SUMMARY ===");
            sb.AppendLine("Symbols tested successfully: " + summary.SymbolsTested + " (errors: " + summary.SymbolsWithErrors + ")");
            sb.AppendLine("Total trades across all symbols: " + summary.TotalTrades);
            sb.AppendLine(summary.SymbolsThatBeatBuyAndHold + " of " + summary.SymbolsTested + " symbols beat buy-and-hold");
            sb.AppendLine("Average strategy return: " + summary.AverageReturnPercent + "%");
            sb.AppendLine("Average buy-and-hold return: " + summary.AverageBuyAndHoldReturnPercent + "%");
            sb.AppendLine("Average win rate: " + summary.AverageWinRate + "%");
            sb.AppendLine("Worst single-symbol drawdown: " + summary.WorstDrawdownPercent + "%");
            sb.AppendLine();
            sb.AppendLine("⚠️ Historical simulation only — past performance does not predict future results.");

            return sb.ToString();
        }
    }
}