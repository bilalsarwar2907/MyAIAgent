namespace MyAIAgent.Models.Requests
{
    /// <summary>
    /// Request body for POST /watchlist. Only the fields a client is allowed to
    /// set — Id / AddedAt are assigned by the server.
    /// </summary>
    public class AddWatchlistItemRequest
    {
        public string UserName { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public string Note { get; set; } = string.Empty;
    }
}
