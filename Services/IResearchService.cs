namespace MyAIAgent.Services
{
    /// <summary>
    /// Runs one or more strategies against a symbol's history and produces a
    /// structured, side-by-side comparison plus text renderings of it.
    /// </summary>
    public interface IResearchService
    {
        Task<SymbolResearchReport> RunResearchAsync(string symbol, IEnumerable<IStrategy> strategies);

        string FormatReport(SymbolResearchReport report);

        string FormatForAI(SymbolResearchReport report);
    }
}
