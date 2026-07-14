// ═══ LOCKED BASE: TechnicalIndicators ═══
// Verified correct as of June 2026 audit. Do not modify without re-running
// the full 59-stock backtest to confirm results are unchanged.
//
// What was fixed: original RSI had an off-by-one error (loop started at
// period+1, leaving index[14] = 0, forcing a buy signal every run).
// Current implementation uses nullable decimal? series -- null entries are
// skipped, never defaulted to 0. Wilder smoothing is correct.
//
// Safe to build on: CalculateRsiSeries, CalculateSmaSeries
// ═════════════════════════════════════════
namespace MyAIAgent.Services
{
    /// <summary>
    /// Local replacements for Alpha Vantage's RSI/SMA endpoints, computed from
    /// raw closing prices. Same math (Wilder-smoothed RSI, simple moving average),
    /// just computed in-process instead of costing an extra API call per symbol.
    ///
    /// All "Series" methods take prices OLDEST-FIRST (matching the order
    /// HistoricalDataService.GetDailyHistoryAsync returns) and return a list of
    /// the same length, aligned index-for-index with the input. Entries before
    /// enough history exists for the indicator are null.
    /// </summary>
    public static class TechnicalIndicators
    {
        /// <summary>
        /// Wilder-smoothed RSI for every day in the series. result[i] is the RSI
        /// as of closes[i], or null if there isn't yet enough history (i &lt; period).
        /// </summary>
        public static List<decimal?> CalculateRsiSeries(List<decimal> closesOldestFirst, int period = 14)
        {
            var result = new List<decimal?>(new decimal?[closesOldestFirst.Count]);

            if (closesOldestFirst.Count <= period)
                return result;

            decimal gainSum = 0, lossSum = 0;
            for (int i = 1; i <= period; i++)
            {
                var change = closesOldestFirst[i] - closesOldestFirst[i - 1];
                if (change > 0) gainSum += change;
                else lossSum += -change;
            }

            decimal avgGain = gainSum / period;
            decimal avgLoss = lossSum / period;
            result[period] = ComputeRsiValue(avgGain, avgLoss);

            for (int i = period + 1; i < closesOldestFirst.Count; i++)
            {
                var change = closesOldestFirst[i] - closesOldestFirst[i - 1];
                decimal gain = change > 0 ? change : 0;
                decimal loss = change < 0 ? -change : 0;

                avgGain = ((avgGain * (period - 1)) + gain) / period;
                avgLoss = ((avgLoss * (period - 1)) + loss) / period;

                result[i] = ComputeRsiValue(avgGain, avgLoss);
            }

            return result;
        }

        /// <summary>
        /// Simple moving average for every day. result[i] is the average of the
        /// trailing `period` values ending at i, or null if i &lt; period - 1.
        /// Works for both price SMA (e.g. 50-day) and a rolling volume average
        /// (e.g. 90-day) since the math is identical — just pass volumes instead
        /// of closes for the volume case.
        /// </summary>
        public static List<decimal?> CalculateSmaSeries(List<decimal> valuesOldestFirst, int period)
        {
            var result = new List<decimal?>(new decimal?[valuesOldestFirst.Count]);
            decimal sum = 0;

            for (int i = 0; i < valuesOldestFirst.Count; i++)
            {
                sum += valuesOldestFirst[i];
                if (i >= period) sum -= valuesOldestFirst[i - period];
                if (i >= period - 1) result[i] = Math.Round(sum / period, 4);
            }

            return result;
        }

        private static decimal ComputeRsiValue(decimal avgGain, decimal avgLoss)
        {
            if (avgLoss == 0) return 100m;
            decimal rs = avgGain / avgLoss;
            return Math.Round(100m - (100m / (1 + rs)), 2);
        }
    }
}