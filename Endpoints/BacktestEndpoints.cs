using MyAIAgent.Services;
using MyAIAgent.Tools;

namespace MyAIAgent.Endpoints
{
    /// <summary>
    /// Backtest runners. The /backtest/* group uses the Yahoo-backed
    /// IBacktestEngine (full history, no daily cap); /backtest/{symbol} and
    /// /backtest/sector/{name} are the legacy Alpha Vantage tool path kept for
    /// the AI chat.
    /// </summary>
    public static class BacktestEndpoints
    {
        public static void MapBacktestEndpoints(this WebApplication app)
        {
            app.MapGet("/backtest/large", async (IBacktestEngine backtestEngine) =>
            {
                var summary = await backtestEngine.RunBatchAsync(StockUniverse.All);
                return Results.Text(backtestEngine.FormatReport(summary), "text/plain");
            });

            app.MapGet("/backtest/large-filtered", async (IBacktestEngine backtestEngine) =>
            {
                var summary = await backtestEngine.RunBatchAsync(StockUniverse.All, useTrendFilter: true);
                return Results.Text(backtestEngine.FormatReport(summary), "text/plain");
            });

            app.MapGet("/backtest/period/{fromYear}/{toYear}", async (int fromYear, int toYear, IBacktestEngine backtestEngine) =>
            {
                var from = new DateTime(fromYear, 1, 1);
                var to = new DateTime(toYear, 12, 31);
                var summary = await backtestEngine.RunBatchRangeAsync(StockUniverse.All, from, to);
                return Results.Text(backtestEngine.FormatReport(summary), "text/plain");
            });

            app.MapGet("/backtest/period/{fromYear}/{toYear}/sector/{sectorName}",
                async (int fromYear, int toYear, string sectorName, IBacktestEngine backtestEngine) =>
                {
                    if (!StockUniverse.BySector.TryGetValue(sectorName.ToLower(), out var symbols))
                        return Results.Text(
                            $"Unknown sector '{sectorName}'. Available: {string.Join(", ", StockUniverse.BySector.Keys)}",
                            "text/plain");

                    var from = new DateTime(fromYear, 1, 1);
                    var to = new DateTime(toYear, 12, 31);
                    var summary = await backtestEngine.RunBatchRangeAsync(symbols, from, to);
                    return Results.Text(backtestEngine.FormatReport(summary), "text/plain");
                });

            app.MapGet("/backtest/sector-v2/{sectorName}", async (string sectorName, IBacktestEngine backtestEngine, bool filtered = false) =>
            {
                if (!StockUniverse.BySector.TryGetValue(sectorName.ToLower(), out var symbols))
                    return Results.Text(
                        $"Unknown sector '{sectorName}'. Available: {string.Join(", ", StockUniverse.BySector.Keys)}",
                        "text/plain");

                var summary = await backtestEngine.RunBatchAsync(symbols, useTrendFilter: filtered);
                return Results.Text(backtestEngine.FormatReport(summary), "text/plain");
            });

            // ── Legacy Alpha Vantage tool path (kept for the AI chat) ──
            app.MapGet("/backtest/{symbol}", async (string symbol, IEnumerable<ITool> tools) =>
            {
                var backtestTool = tools.FirstOrDefault(t => t.Name == "BacktestStrategy");
                if (backtestTool == null) return Results.Problem("Backtest tool not available.");
                return Results.Ok(new { symbol = symbol.ToUpper(), result = await backtestTool.ExecuteAsync(symbol) });
            });

            app.MapGet("/backtest/sector/{sectorName}", async (string sectorName, IEnumerable<ITool> tools) =>
            {
                var backtestTool = tools.FirstOrDefault(t => t.Name == "BacktestStrategy") as BacktestTool;
                if (backtestTool == null) return Results.Problem("Backtest tool not available.");
                return Results.Ok(new { sector = sectorName, result = await backtestTool.ExecuteSectorAsync(sectorName) });
            });
        }
    }
}
