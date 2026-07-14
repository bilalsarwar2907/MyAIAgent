namespace MyAIAgent.Models
{
    public class PortfolioItem
    {
        public int Id { get; set; }

        // Which user owns this
        public string UserName { get; set; } = string.Empty;

        // Stock symbol e.g. AAPL
        public string Symbol { get; set; } = string.Empty;

        // Number of shares owned
        public decimal Shares { get; set; }

        // Price paid per share
        public decimal BuyPrice { get; set; }

        // Optional note e.g. "Long term hold"
        public string Note { get; set; } = string.Empty;

        // When it was added
        public DateTime BoughtAt { get; set; } = DateTime.UtcNow;

        // Calculated fields (not stored in DB)
        public decimal TotalInvested => Shares * BuyPrice;
    }
}