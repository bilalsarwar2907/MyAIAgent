using MyAIAgent.Models;
using MyAIAgent.Services;

namespace MyAIAgent.Endpoints
{
    /// <summary>
    /// POST /chat — keyword/regex pre-routing to a tool (analysis, price, note,
    /// news, research), a portfolio-aware LLM path, then a plain LLM fallback.
    /// Matching heuristics live in <see cref="ChatIntentRouter"/>; this keeps the
    /// original sequential fall-through so behaviour is unchanged.
    /// </summary>
    public static class ChatEndpoints
    {
        public static void MapChatEndpoints(this WebApplication app)
        {
            app.MapPost("/chat", async (ChatRequestV2 request, IAiService ai, IEnumerable<ITool> tools) =>
            {
                if (string.IsNullOrWhiteSpace(request.Message))
                    return Results.BadRequest("Message cannot be empty.");

                if (string.IsNullOrWhiteSpace(request.ConversationId))
                    return Results.BadRequest("ConversationId is required.");

                var messageLower = request.Message.ToLower();

                object ToolResult(string toolName, string result) => new
                {
                    toolUsed = true,
                    tool = toolName,
                    result,
                    conversationId = request.ConversationId
                };

                if (ChatIntentRouter.IsAnalysisQuery(messageLower))
                {
                    // Fast-path: pull symbols straight from the text, skipping the slow DecideTool call.
                    var extractedInput = ChatIntentRouter.ExtractAnalysisSymbols(request.Message);
                    if (!string.IsNullOrWhiteSpace(extractedInput))
                    {
                        var analysisTool = tools.FirstOrDefault(t => t.Name == "AnalyzeStock");
                        if (analysisTool != null)
                        {
                            var stockData = await analysisTool.ExecuteAsync(extractedInput);
                            var analysis = await ai.AnalyzeStocks(stockData, request.Message);
                            await ai.SaveToolMessage(request.Message, analysis, request.ConversationId, request.UserName);
                            return Results.Ok(ToolResult("AnalyzeStock", analysis));
                        }
                    }

                    // Fallback: let DecideTool try when the regex found no symbol.
                    var toolDecision = await ai.DecideTool(request.Message);
                    if (toolDecision.UseTool && toolDecision.ToolName == "AnalyzeStock")
                    {
                        var analysisTool = tools.FirstOrDefault(t => t.Name == "AnalyzeStock");
                        if (analysisTool != null)
                        {
                            var stockData = await analysisTool.ExecuteAsync(toolDecision.ToolInput);
                            var analysis = await ai.AnalyzeStocks(stockData, request.Message);
                            await ai.SaveToolMessage(request.Message, analysis, request.ConversationId, request.UserName);
                            return Results.Ok(ToolResult("AnalyzeStock", analysis));
                        }
                    }
                }

                if (ChatIntentRouter.IsStockQuery(messageLower))
                {
                    var toolDecision = await ai.DecideTool(request.Message);
                    if (toolDecision.UseTool)
                    {
                        var tool = tools.FirstOrDefault(t => t.Name == toolDecision.ToolName);
                        if (tool != null)
                        {
                            var result = await tool.ExecuteAsync(toolDecision.ToolInput);
                            await ai.SaveToolMessage(request.Message, result, request.ConversationId, request.UserName);
                            return Results.Ok(ToolResult(tool.Name, result));
                        }
                    }
                }

                if (ChatIntentRouter.IsNoteQuery(messageLower))
                {
                    var toolDecision = await ai.DecideTool(request.Message);
                    if (toolDecision.UseTool)
                    {
                        var tool = tools.FirstOrDefault(t => t.Name == toolDecision.ToolName);
                        if (tool != null)
                        {
                            var result = await tool.ExecuteAsync(toolDecision.ToolInput);
                            await ai.SaveToolMessage(request.Message, result, request.ConversationId, request.UserName);
                            return Results.Ok(ToolResult(tool.Name, result));
                        }
                    }
                }

                if (ChatIntentRouter.IsNewsQuery(messageLower))
                {
                    var toolDecision = await ai.DecideTool(request.Message);
                    if (toolDecision.UseTool && toolDecision.ToolName == "GetStockNews")
                    {
                        var tool = tools.FirstOrDefault(t => t.Name == "GetStockNews");
                        if (tool != null)
                        {
                            var result = await tool.ExecuteAsync(toolDecision.ToolInput);
                            await ai.SaveToolMessage(request.Message, result, request.ConversationId, request.UserName);
                            return Results.Ok(ToolResult(tool.Name, result));
                        }
                    }
                }

                if (ChatIntentRouter.IsResearchQuery(messageLower))
                {
                    var symbol = ChatIntentRouter.ExtractResearchSymbol(request.Message);
                    if (symbol != null)
                    {
                        var tool = tools.FirstOrDefault(t => t.Name == "ResearchStock");
                        if (tool != null)
                        {
                            var researchPrompt = await tool.ExecuteAsync(symbol);
                            var explanation = await ai.InterpretResearch(researchPrompt);
                            await ai.SaveToolMessage(request.Message, explanation, request.ConversationId, request.UserName);
                            return Results.Ok(ToolResult("ResearchStock", explanation));
                        }
                    }
                }

                // Portfolio-aware path — injects the daily_agent report into the system prompt.
                if (ChatIntentRouter.IsPortfolioQuery(messageLower))
                {
                    try
                    {
                        var reply = await ai.AskAIWithPortfolioContext(request.Message, request.ConversationId, request.UserName);
                        return Results.Ok(new { toolUsed = false, conversationId = request.ConversationId, message = reply });
                    }
                    catch (Exception ex)
                    {
                        return Results.Problem("AI service error: " + ex.Message);
                    }
                }

                try
                {
                    var reply = await ai.AskAI(request.Message, request.ConversationId, request.UserName);
                    return Results.Ok(new { toolUsed = false, conversationId = request.ConversationId, message = reply });
                }
                catch (Exception ex)
                {
                    return Results.Problem("AI service error: " + ex.Message);
                }
            });
        }
    }
}
