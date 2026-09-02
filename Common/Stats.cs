namespace MyAIAgent.Common
{
    /// <summary>
    /// Small numeric helpers shared across research/screener/backtest code so the
    /// same statistic isn't re-implemented (slightly differently) in five places.
    /// </summary>
    public static class Stats
    {
        /// <summary>
        /// Median of a sequence. Returns 0 for an empty sequence.
        /// Even counts average the two middle values; matches the hand-rolled
        /// median logic previously duplicated in Program.cs and the services.
        /// </summary>
        public static decimal Median(IEnumerable<decimal> values, int round = 2)
        {
            var sorted = values.OrderBy(x => x).ToList();
            if (sorted.Count == 0) return 0;

            int mid = sorted.Count / 2;
            decimal median = sorted.Count % 2 != 0
                ? sorted[mid]
                : (sorted[mid - 1] + sorted[mid]) / 2;

            return Math.Round(median, round);
        }
    }
}
