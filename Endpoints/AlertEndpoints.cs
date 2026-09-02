using MyAIAgent.Data;
using MyAIAgent.Models;
using MyAIAgent.Models.Requests;
using MyAIAgent.Services;

namespace MyAIAgent.Endpoints
{
    public static class AlertEndpoints
    {
        public static void MapAlertEndpoints(this WebApplication app)
        {
            app.MapGet("/alerts/{userName}", (string userName, AppDbContext db) =>
            {
                var alerts = db.PriceAlerts
                    .Where(x => x.UserName == userName)
                    .OrderByDescending(x => x.CreatedAt)
                    .ToList();
                return Results.Ok(alerts);
            });

            app.MapPost("/alerts", async (CreatePriceAlertRequest request, AppDbContext db) =>
            {
                if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Symbol))
                    return Results.BadRequest("UserName and Symbol are required.");

                if (request.TargetPrice <= 0)
                    return Results.BadRequest("Target price must be greater than 0.");

                if (request.Direction != "above" && request.Direction != "below")
                    return Results.BadRequest("Direction must be 'above' or 'below'.");

                var alert = new PriceAlert
                {
                    UserName = request.UserName,
                    Symbol = request.Symbol.ToUpper(),
                    TargetPrice = request.TargetPrice,
                    Direction = request.Direction,
                    CreatedAt = DateTime.UtcNow,
                    IsTriggered = false
                };

                db.PriceAlerts.Add(alert);
                await db.SaveChangesAsync();

                return Results.Ok(new { message = "Alert created for " + alert.Symbol, alert });
            });

            app.MapDelete("/alerts/{id}", async (int id, AppDbContext db) =>
            {
                var alert = db.PriceAlerts.FirstOrDefault(x => x.Id == id);
                if (alert == null) return Results.NotFound("Alert not found.");
                db.PriceAlerts.Remove(alert);
                await db.SaveChangesAsync();
                return Results.Ok(new { message = "Alert removed." });
            });

            // Check all active alerts against live prices. Called from the frontend
            // periodically (e.g. every 60s).
            app.MapPost("/alerts/check/{userName}", async (string userName, AppDbContext db, IEnumerable<ITool> tools) =>
            {
                var stockTool = tools.FirstOrDefault(t => t.Name == "GetStockPrice");
                if (stockTool == null) return Results.Problem("Stock tool not available.");

                var activeAlerts = db.PriceAlerts
                    .Where(x => x.UserName == userName && !x.IsTriggered)
                    .ToList();

                var newlyTriggered = new List<PriceAlert>();

                // Group by symbol to avoid duplicate API calls for the same stock
                var symbolGroups = activeAlerts.GroupBy(a => a.Symbol);

                foreach (var group in symbolGroups)
                {
                    var symbol = group.Key;
                    var raw = await stockTool.ExecuteAsync(symbol);

                    // Extract price from formatted string "💰 Price: $291.13"
                    var match = System.Text.RegularExpressions.Regex.Match(raw, @"Price:\s*\$?([\d.]+)");
                    if (!match.Success) continue;

                    var currentPrice = decimal.Parse(match.Groups[1].Value, System.Globalization.CultureInfo.InvariantCulture);

                    foreach (var alert in group)
                    {
                        bool shouldTrigger =
                            (alert.Direction == "above" && currentPrice >= alert.TargetPrice) ||
                            (alert.Direction == "below" && currentPrice <= alert.TargetPrice);

                        if (shouldTrigger)
                        {
                            alert.IsTriggered = true;
                            alert.TriggeredPrice = currentPrice;
                            alert.TriggeredAt = DateTime.UtcNow;
                            newlyTriggered.Add(alert);
                        }
                    }

                    // Small delay between API calls to respect rate limits
                    await Task.Delay(1200);
                }

                if (newlyTriggered.Count > 0)
                {
                    await db.SaveChangesAsync();
                }

                return Results.Ok(new
                {
                    alertsChecked = activeAlerts.Count,
                    triggered = newlyTriggered
                });
            });
        }
    }
}
