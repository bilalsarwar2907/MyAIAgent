namespace MyAIAgent.Models
{
    public class PriceAlert
    {
        public int Id { get; set; }

        // Which user owns this alert
        public string UserName { get; set; } = string.Empty;

        // Stock symbol e.g. AAPL
        public string Symbol { get; set; } = string.Empty;

        // The price the user is watching for
        public decimal TargetPrice { get; set; }

        // "above" = alert when price goes ABOVE target
        // "below" = alert when price goes BELOW target
        public string Direction { get; set; } = "above";

        // Has this alert already been triggered?
        public bool IsTriggered { get; set; } = false;

        // The price at the moment it triggered (for display)
        public decimal? TriggeredPrice { get; set; }

        // When it triggered
        public DateTime? TriggeredAt { get; set; }

        // When the alert was created
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}