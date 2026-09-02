namespace MyAIAgent.Services
{
    /// <summary>
    /// Abstraction over the daily price-history provider (currently Yahoo Finance v8).
    /// Depend on this, not the concrete class, so callers can be unit-tested
    /// without network access.
    /// </summary>
    public interface IHistoricalDataService
    {
        Task<List<DailyBar>> GetDailyHistoryAsync(string symbol);

        Task<List<DailyBar>> GetDailyHistoryRangeAsync(string symbol, DateTime fromDate, DateTime toDate);

        Task<Dictionary<string, List<DailyBar>>> GetDailyHistoryForManyAsync(
            IEnumerable<string> symbols, int delayMs = 300);
    }
}
