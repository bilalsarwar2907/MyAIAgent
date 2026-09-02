using MyAIAgent.Data;
using MyAIAgent.Models;
using MyAIAgent.Models.Requests;

namespace MyAIAgent.Endpoints
{
    public static class PortfolioEndpoints
    {
        public static void MapPortfolioEndpoints(this WebApplication app)
        {
            app.MapGet("/portfolio/{userName}", (string userName, AppDbContext db) =>
            {
                var items = db.PortfolioItems
                    .Where(x => x.UserName == userName)
                    .OrderByDescending(x => x.BoughtAt)
                    .ToList();
                return Results.Ok(items);
            });

            app.MapPost("/portfolio", async (AddPortfolioItemRequest request, AppDbContext db) =>
            {
                if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Symbol))
                    return Results.BadRequest("UserName and Symbol are required.");
                if (request.Shares <= 0) return Results.BadRequest("Shares must be greater than 0.");
                if (request.BuyPrice <= 0) return Results.BadRequest("Buy price must be greater than 0.");

                var item = new PortfolioItem
                {
                    UserName = request.UserName,
                    Symbol = request.Symbol.ToUpper(),
                    Shares = request.Shares,
                    BuyPrice = request.BuyPrice,
                    Note = request.Note ?? string.Empty,
                    BoughtAt = DateTime.UtcNow
                };
                db.PortfolioItems.Add(item);
                await db.SaveChangesAsync();
                return Results.Ok(new { message = item.Symbol + " added to portfolio.", item });
            });

            app.MapDelete("/portfolio/{id}", async (int id, AppDbContext db) =>
            {
                var item = db.PortfolioItems.FirstOrDefault(x => x.Id == id);
                if (item == null) return Results.NotFound("Item not found.");
                db.PortfolioItems.Remove(item);
                await db.SaveChangesAsync();
                return Results.Ok(new { message = item.Symbol + " removed from portfolio." });
            });
        }
    }
}
