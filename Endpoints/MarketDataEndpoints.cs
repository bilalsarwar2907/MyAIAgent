using MyAIAgent.Models;
using MyAIAgent.Services;
using MyAIAgent.Tools;

namespace MyAIAgent.Endpoints
{
    /// <summary>
    /// Ad-hoc single-symbol lookups backed by the tool layer:
    /// price (/stock), rule-based decision table (/decision), news (/news),
    /// and the AI-narrated multi-symbol analysis (/analyze).
    /// </summary>
    public static class MarketDataEndpoints
    {
        public static void MapMarketDataEndpoints(this WebApplication app)
        {
            app.MapGet("/stock/{symbol}", async (string symbol, IEnumerable<ITool> tools) =>
            {
                var stockTool = tools.FirstOrDefault(t => t.Name == "GetStockPrice");
                if (stockTool == null) return Results.Problem("Stock tool not available.");
                return Results.Ok(new { symbol = symbol.ToUpper(), result = await stockTool.ExecuteAsync(symbol) });
            });

            app.MapGet("/decision/{symbol}", async (string symbol, IEnumerable<ITool> tools) =>
            {
                var analysisTool = tools.FirstOrDefault(t => t.Name == "AnalyzeStock") as StockAnalysisTool;
                if (analysisTool == null) return Results.Problem("Analysis tool not available.");

                var table = await analysisTool.BuildDecisionTableAsync(symbol.Trim().ToUpper());
                return Results.Ok(table);
            });

            app.MapGet("/news/{symbol}", async (string symbol, IEnumerable<ITool> tools) =>
            {
                var newsTool = tools.FirstOrDefault(t => t.Name == "GetStockNews");
                if (newsTool == null) return Results.Problem("News tool not available.");
                var result = await newsTool.ExecuteAsync(symbol);
                return Results.Ok(new { symbol = symbol.ToUpper(), result });
            });

            app.MapPost("/analyze", async (AnalyzeRequest request, IAiService ai, IEnumerable<ITool> tools) =>
            {
                if (string.IsNullOrWhiteSpace(request.Symbols))
                    return Results.BadRequest("Symbols are required.");

                var analysisTool = tools.FirstOrDefault(t => t.Name == "AnalyzeStock");
                if (analysisTool == null) return Results.Problem("Analysis tool not available.");

                var stockData = await analysisTool.ExecuteAsync(request.Symbols);
                var userQuestion = string.IsNullOrWhiteSpace(request.Question)
                    ? "Analyze these stocks and tell me which looks strongest right now."
                    : request.Question;

                try
                {
                    var analysis = await ai.AnalyzeStocks(stockData, userQuestion);
                    return Results.Ok(new { symbols = request.Symbols.ToUpper(), rawData = stockData, analysis });
                }
                catch (Exception ex)
                {
                    return Results.Problem("Analysis error: " + ex.Message);
                }
            });
        }
    }
}
