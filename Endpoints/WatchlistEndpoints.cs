using MyAIAgent.Data;
using MyAIAgent.Models;
using MyAIAgent.Models.Requests;

namespace MyAIAgent.Endpoints
{
    public static class WatchlistEndpoints
    {
        public static void MapWatchlistEndpoints(this WebApplication app)
        {
            app.MapGet("/watchlist/{userName}", (string userName, AppDbContext db) =>
            {
                var items = db.WatchlistItems
                    .Where(x => x.UserName == userName)
                    .OrderByDescending(x => x.AddedAt)
                    .ToList();
                return Results.Ok(items);
            });

            app.MapPost("/watchlist", async (AddWatchlistItemRequest request, AppDbContext db) =>
            {
                if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Symbol))
                    return Results.BadRequest("UserName and Symbol are required.");

                var symbol = request.Symbol.ToUpper();
                var exists = db.WatchlistItems.Any(x => x.UserName == request.UserName && x.Symbol == symbol);
                if (exists)
                    return Results.BadRequest(symbol + " is already in your watchlist.");

                var item = new WatchlistItem
                {
                    UserName = request.UserName,
                    Symbol = symbol,
                    Note = request.Note ?? string.Empty,
                    AddedAt = DateTime.UtcNow
                };
                db.WatchlistItems.Add(item);
                await db.SaveChangesAsync();
                return Results.Ok(new { message = item.Symbol + " added to watchlist.", item });
            });

            app.MapDelete("/watchlist/{id}", async (int id, AppDbContext db) =>
            {
                var item = db.WatchlistItems.FirstOrDefault(x => x.Id == id);
                if (item == null) return Results.NotFound("Item not found.");
                db.WatchlistItems.Remove(item);
                await db.SaveChangesAsync();
                return Results.Ok(new { message = item.Symbol + " removed from watchlist." });
            });
        }
    }
}
