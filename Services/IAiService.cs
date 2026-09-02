using MyAIAgent.Models;

namespace MyAIAgent.Services
{
    /// <summary>
    /// Orchestrates calls to the local LLM: normal chat with conversation
    /// memory, stock/research interpretation, tool-need decisions, and
    /// portfolio-aware chat.
    /// </summary>
    public interface IAiService
    {
        Task SaveToolMessage(string userMessage, string toolResult, string conversationId, string userName);

        Task<string> AskAI(string userMessage, string conversationId, string userName);

        Task<string> AnalyzeStocks(string stockData, string userQuestion);

        Task<string> InterpretResearch(string researchPrompt);

        Task<ToolResponse> DecideTool(string userMessage);

        Task<string> AskAIWithPortfolioContext(string userMessage, string conversationId, string userName);
    }
}
