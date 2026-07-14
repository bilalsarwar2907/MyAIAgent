namespace MyAIAgent.Models
{
    public class AnalyzeRequest
    {
        // Comma-separated stock symbols e.g. "AAPL" or "AAPL,MSFT,GOOGL"
        public string Symbols { get; set; } = string.Empty;

        // Optional question e.g. "Which one should I buy?"
        public string Question { get; set; } = string.Empty;
    }
}