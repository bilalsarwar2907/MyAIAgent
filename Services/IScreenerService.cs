namespace MyAIAgent.Services
{
    /// <summary>
    /// Lightweight RSI-candidate screener: 10y B&amp;H return, trend bucket,
    /// current RSI + slope, and the Finding #1 exclusion rule.
    /// </summary>
    public interface IScreenerService
    {
        Task<ScreenerResult> RunAsync(IEnumerable<string> symbols);
    }
}
