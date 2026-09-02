using MyAIAgent.Models;

namespace MyAIAgent.Services
{
    /// <summary>
    /// Tracks paper (simulated) trades: open, close with an auto-fetched
    /// buy-and-hold benchmark, summarise, and delete open positions.
    /// </summary>
    public interface IPaperPortfolioService
    {
        Task<PaperTrade> OpenTradeAsync(OpenTradeRequest req);

        Task<PaperTrade?> CloseTradeAsync(CloseTradeRequest req);

        Task<PortfolioSummary> GetSummaryAsync(string userName, List<PriceUpdate>? livePrices = null);

        Task<bool> DeleteTradeAsync(int tradeId, string userName);
    }
}
