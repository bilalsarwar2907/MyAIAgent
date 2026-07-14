namespace MyAIAgent.Models
{
    public class WatchlistItem
    {
        public int Id { get; set; }

        // Which user owns this watchlist item
        public string UserName { get; set; } = string.Empty;

        // Stock symbol e.g. AAPL, TSLA
        public string Symbol { get; set; } = string.Empty;

        // Optional note e.g. "Buy if drops below $280"
        public string Note { get; set; } = string.Empty;

        // When it was added
        public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    }
}