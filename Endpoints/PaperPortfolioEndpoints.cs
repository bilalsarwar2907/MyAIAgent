using MyAIAgent.Services;

namespace MyAIAgent.Endpoints
{
    /// <summary>
    /// Paper (simulated) trade tracking: list with P&amp;L, open, close (auto B&amp;H
    /// benchmark), and delete of open positions. All logic lives in
    /// IPaperPortfolioService — these are thin validation + mapping.
    /// </summary>
    public static class PaperPortfolioEndpoints
    {
        public static void MapPaperPortfolioEndpoints(this WebApplication app)
        {
            app.MapGet("/api/paper/{userName}", async (string userName, IPaperPortfolioService svc) =>
            {
                var summary = await svc.GetSummaryAsync(userName);
                return Results.Json(summary);
            });

            app.MapPost("/api/paper/{userName}/prices",
                async (string userName, List<PriceUpdate> prices, IPaperPortfolioService svc) =>
                {
                    var summary = await svc.GetSummaryAsync(userName, prices);
                    return Results.Json(summary);
                });

            app.MapPost("/api/paper/open", async (OpenTradeRequest req, IPaperPortfolioService svc) =>
            {
                if (string.IsNullOrWhiteSpace(req.UserName) || string.IsNullOrWhiteSpace(req.Symbol))
                    return Results.BadRequest(new { error = "UserName and Symbol are required." });
                if (req.EntryPrice <= 0)
                    return Results.BadRequest(new { error = "EntryPrice must be greater than 0." });
                if (req.RsiAtEntry < 0 || req.RsiAtEntry > 100)
                    return Results.BadRequest(new { error = "RsiAtEntry must be between 0 and 100." });

                var trade = await svc.OpenTradeAsync(req);
                return Results.Ok(new { message = $"Paper trade opened for {req.Symbol.ToUpper()}.", trade });
            });

            app.MapPost("/api/paper/close", async (CloseTradeRequest req, IPaperPortfolioService svc) =>
            {
                if (req.ExitPrice <= 0)
                    return Results.BadRequest(new { error = "ExitPrice must be greater than 0." });

                var trade = await svc.CloseTradeAsync(req);
                if (trade == null)
                    return Results.NotFound(new { error = "Trade not found, already closed, or not owned by this user." });

                return Results.Ok(new { message = $"{trade.Symbol} trade closed.", trade });
            });

            app.MapDelete("/api/paper/{tradeId}/{userName}", async (int tradeId, string userName, IPaperPortfolioService svc) =>
            {
                var deleted = await svc.DeleteTradeAsync(tradeId, userName);
                if (!deleted)
                    return Results.NotFound(new { error = "Trade not found, already closed, or not owned by this user." });
                return Results.Ok(new { message = "Paper trade deleted." });
            });
        }
    }
}
