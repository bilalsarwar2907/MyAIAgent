namespace MyAIAgent.Configuration
{
    /// <summary>
    /// Bound from the "Trading" config section. Paths / knobs for the
    /// agentic trading layer that the C# API reads (written by daily_agent.py).
    /// </summary>
    public class TradingOptions
    {
        public const string SectionName = "Trading";

        /// <summary>
        /// Absolute path to trading_output.txt produced by daily_agent.py.
        /// Injected into the chat system prompt for portfolio-aware answers.
        /// Empty string disables portfolio context injection.
        /// </summary>
        public string PortfolioReportPath { get; set; } = "";
    }
}
