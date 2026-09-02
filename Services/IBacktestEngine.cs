namespace MyAIAgent.Services
{
    /// <summary>
    /// Runs the RSI mean-reversion simulation across one or many symbols.
    /// </summary>
    public interface IBacktestEngine
    {
        Task<BacktestSummary> RunBatchAsync(
            IEnumerable<string> symbols,
            int rsiBuyThreshold = 30,
            int rsiSellThreshold = 70,
            bool useTrendFilter = false);

        Task<BacktestSummary> RunBatchRangeAsync(
            IEnumerable<string> symbols,
            DateTime fromDate,
            DateTime toDate,
            int rsiBuyThreshold = 30,
            int rsiSellThreshold = 70,
            bool useTrendFilter = false);

        Task<SymbolBacktestResult> RunSingleAsync(
            string symbol,
            int rsiBuyThreshold = 30,
            int rsiSellThreshold = 70,
            bool useTrendFilter = false);

        Task<SymbolBacktestResult> RunSingleRangeAsync(
            string symbol,
            DateTime fromDate,
            DateTime toDate,
            int rsiBuyThreshold = 30,
            int rsiSellThreshold = 70,
            bool useTrendFilter = false);

        string FormatReport(BacktestSummary summary);
    }
}
