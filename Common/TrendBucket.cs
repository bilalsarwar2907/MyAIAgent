namespace MyAIAgent.Common
{
    /// <summary>
    /// Maps a 10-year buy-and-hold return to the trend-strength bucket used by
    /// the screener, factor research, and the RSI lookup endpoint. Single source
    /// of truth for the &lt;100% / 100–300% / &gt;300% thresholds.
    /// </summary>
    public static class TrendBucket
    {
        public const string Weak = "Weak (<100%)";
        public const string Medium = "Medium (100–300%)";
        public const string Strong = "Strong (>300%)";

        public static string For(decimal buyAndHoldReturnPercent) =>
            buyAndHoldReturnPercent < 100 ? Weak
          : buyAndHoldReturnPercent < 300 ? Medium
                                          : Strong;
    }
}
