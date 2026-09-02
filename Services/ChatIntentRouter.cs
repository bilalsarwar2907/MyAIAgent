using System.Text.RegularExpressions;

namespace MyAIAgent.Services
{
    /// <summary>
    /// Pure keyword / regex heuristics the /chat endpoint uses to decide whether
    /// a message should be handled by a tool before falling back to the LLM.
    /// Extracted from Program.cs so the matching rules can be unit-tested; the
    /// endpoint keeps the sequential fall-through (a query can match more than
    /// one intent and the first that produces a result wins).
    /// </summary>
    public static class ChatIntentRouter
    {
        public static bool IsAnalysisQuery(string lower) =>
            lower.Contains("analyze") ||
            lower.Contains("analyse") ||
            lower.Contains("compare") ||
            lower.Contains("recommend") ||
            lower.Contains("should i buy") ||
            lower.Contains("should i sell") ||
            lower.Contains("which is better") ||
            lower.Contains("vs") ||
            lower.Contains("versus");

        public static bool IsStockQuery(string lower) =>
            lower.Contains("price of") ||
            lower.Contains("stock price") ||
            lower.Contains("how much is");

        public static bool IsNoteQuery(string lower) =>
            lower.Contains("remember") ||
            lower.Contains("save") ||
            lower.Contains("note");

        public static bool IsNewsQuery(string lower) =>
            lower.Contains("news") ||
            lower.Contains("headlines") ||
            lower.Contains("latest on");

        public static bool IsResearchQuery(string lower) =>
            lower.Contains("research") ||
            lower.Contains("backtest") ||
            lower.Contains("historical") ||
            lower.Contains("how did") ||
            lower.Contains("strategy") ||
            lower.Contains("how has") ||
            lower.Contains("performance");

        public static bool IsPortfolioQuery(string lower) =>
            lower.Contains("my trade") ||
            lower.Contains("my position") ||
            lower.Contains("my portfolio") ||
            lower.Contains("open position") ||
            lower.Contains("how am i doing") ||
            lower.Contains("how are my") ||
            lower.Contains("ibm") ||
            lower.Contains("intc") ||
            lower.Contains("exit trigger") ||
            lower.Contains("should i hold") ||
            lower.Contains("should i exit") ||
            lower.Contains("p&l") ||
            lower.Contains("my rsi");

        /// <summary>
        /// Fast-path symbol extraction for "analyze/compare/recommend AAPL, MSFT ..."
        /// — returns a comma-separated upper-case list, or "" if nothing matched.
        /// </summary>
        public static string ExtractAnalysisSymbols(string message)
        {
            var actionMatch = Regex.Match(
                message,
                @"(?:analyze|analyse|compare|recommend)\s+((?:[A-Z]{1,5}[,\s]*)+?)(?:\s+(?:and|vs|versus|against|with|give|for|please)|$)",
                RegexOptions.IgnoreCase);

            if (!actionMatch.Success) return "";

            return string.Join(",",
                actionMatch.Groups[1].Value
                    .Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Where(w => w.Length >= 1 && w.Length <= 5 && w.All(char.IsLetter))
                    .Select(w => w.ToUpper()));
        }

        private static readonly HashSet<string> NonTickerWords = new()
        {
            "AI", "US", "UK", "EU", "RSI", "SMA", "EMA", "EV", "ETF", "GDP",
            "CEO", "IPO", "PE", "EPS", "YOY", "QOQ", "MOM", "ATH", "ATL",
            "NYSE", "NASDAQ", "SP", "DOW", "FED", "SEC", "IRS", "ESG",
            "FAQ", "API", "URL", "JSON", "SQL", "CSS", "HTML", "UX"
        };

        /// <summary>
        /// First 2–5 letter upper-case token in the message that isn't a known
        /// non-ticker acronym, or null. phi3:mini is unreliable at picking the
        /// ticker itself, so research queries extract it here.
        /// </summary>
        public static string? ExtractResearchSymbol(string message)
        {
            var symbolMatch = Regex.Match(message, @"\b([A-Z]{2,5})\b");
            while (symbolMatch.Success && NonTickerWords.Contains(symbolMatch.Groups[1].Value))
                symbolMatch = symbolMatch.NextMatch();

            return symbolMatch.Success ? symbolMatch.Groups[1].Value : null;
        }
    }
}
