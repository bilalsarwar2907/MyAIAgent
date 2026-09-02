using MyAIAgent.Services;
using MyAIAgent.Tools;

namespace MyAIAgent.Endpoints
{
    /// <summary>
    /// Strategy-comparison research. Single-symbol reports use IResearchService
    /// directly; the sector / all-sectors / trend-strength aggregates are
    /// delegated to IResearchQueryService.
    /// </summary>
    public static class ResearchEndpoints
    {
        private static List<IStrategy> DefaultStrategies() => new()
        {
            new RsiStrategy(30, 70),
            new RsiStrategy(30, 70, trendFilter: true)
        };

        public static void MapResearchEndpoints(this WebApplication app)
        {
            app.MapGet("/research/{symbol}", async (string symbol, IResearchService researchService) =>
            {
                var report = await researchService.RunResearchAsync(symbol, DefaultStrategies());
                return Results.Text(researchService.FormatReport(report), "text/plain");
            });

            app.MapGet("/research/{symbol}/explain", async (string symbol, IAiService ai, IResearchService researchService, IEnumerable<ITool> tools) =>
            {
                var report = await researchService.RunResearchAsync(symbol, DefaultStrategies());

                if (!string.IsNullOrEmpty(report.Error))
                    return Results.Json(new { error = report.Error });

                var researchTool = tools.FirstOrDefault(t => t.Name == "ResearchStock") as StockResearchTool;
                var prompt = researchTool != null
                    ? await researchTool.ExecuteAsync(symbol)
                    : researchService.FormatForAI(report);

                var explanation = await ai.InterpretResearch(prompt);
                return Results.Json(new { symbol = symbol.ToUpper(), explanation });
            });

            app.MapGet("/research/batch/{sector}", async (string sector, IResearchService researchService) =>
            {
                if (!StockUniverse.BySector.TryGetValue(sector.ToLower(), out var symbols))
                    return Results.Text(
                        $"Unknown sector '{sector}'. Available: {string.Join(", ", StockUniverse.BySector.Keys)}",
                        "text/plain");

                var strategies = DefaultStrategies();

                var sb = new System.Text.StringBuilder();
                foreach (var symbol in symbols)
                {
                    var report = await researchService.RunResearchAsync(symbol, strategies);
                    sb.AppendLine(researchService.FormatReport(report));
                    sb.AppendLine(new string('─', 60));
                }

                return Results.Text(sb.ToString(), "text/plain");
            });

            app.MapGet("/research/sector/{sectorName}", async (string sectorName, IResearchQueryService query) =>
                Results.Json(await query.SectorSummaryAsync(sectorName)));

            app.MapGet("/research/all-sectors", async (IResearchQueryService query) =>
                Results.Json(await query.AllSectorsAsync()));

            app.MapGet("/research/factor/trend-strength", async (IResearchQueryService query) =>
                Results.Json(await query.TrendStrengthFactorAsync()));

            app.MapGet("/research/factor/trend-strength/{fromYear}/{toYear}",
                async (int fromYear, int toYear, IResearchQueryService query) =>
                    Results.Json(await query.TrendStrengthFactorRangeAsync(fromYear, toYear)));
        }
    }
}
