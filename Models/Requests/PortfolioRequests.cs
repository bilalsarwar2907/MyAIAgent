namespace MyAIAgent.Models.Requests
{
    /// <summary>
    /// Request body for POST /portfolio. Id / BoughtAt / TotalInvested are
    /// server-controlled and not accepted from the client.
    /// </summary>
    public class AddPortfolioItemRequest
    {
        public string UserName { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public decimal Shares { get; set; }
        public decimal BuyPrice { get; set; }
        public string Note { get; set; } = string.Empty;
    }
}
