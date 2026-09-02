namespace MyAIAgent.Models.Requests
{
    /// <summary>
    /// Request body for POST /alerts. The trigger state (IsTriggered,
    /// TriggeredPrice, TriggeredAt), Id and CreatedAt are all server-controlled.
    /// </summary>
    public class CreatePriceAlertRequest
    {
        public string UserName { get; set; } = string.Empty;
        public string Symbol { get; set; } = string.Empty;
        public decimal TargetPrice { get; set; }
        public string Direction { get; set; } = "above";
    }
}
