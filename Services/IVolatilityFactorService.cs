namespace MyAIAgent.Services
{
    /// <summary>
    /// Tests whether annualised volatility predicts RSI strategy effectiveness,
    /// bucketing results into Low / Medium / High volatility bands.
    /// </summary>
    public interface IVolatilityFactorService
    {
        Task<VolatilityFactorResult> RunAsync(IEnumerable<string> symbols);

        Task<VolatilityFactorResult> RunRangeAsync(
            IEnumerable<string> symbols, DateTime fromDate, DateTime toDate);
    }
}
