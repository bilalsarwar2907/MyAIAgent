using MyAIAgent.Data;

namespace MyAIAgent.Endpoints
{
    /// <summary>List / read / delete stored chat conversations for a user.</summary>
    public static class ConversationEndpoints
    {
        public static void MapConversationEndpoints(this WebApplication app)
        {
            app.MapGet("/conversations/{userName}", (string userName, AppDbContext db) =>
            {
                var conversations = db.ChatMessages
                    .Where(x => x.UserName == userName)
                    .GroupBy(x => x.ConversationId)
                    .Select(g => new
                    {
                        conversationId = g.Key,
                        lastMessage = g.OrderByDescending(m => m.Id).Select(m => m.Content).FirstOrDefault(),
                        lastUpdated = g.OrderByDescending(m => m.Id).Select(m => m.CreatedAt).FirstOrDefault(),
                        messageCount = g.Count()
                    })
                    .OrderByDescending(c => c.lastUpdated)
                    .ToList();

                return Results.Ok(conversations);
            });

            app.MapGet("/conversations/{userName}/{conversationId}", (string userName, string conversationId, AppDbContext db) =>
            {
                var messages = db.ChatMessages
                    .Where(x => x.UserName == userName && x.ConversationId == conversationId)
                    .OrderBy(x => x.Id)
                    .ToList();

                return Results.Ok(messages);
            });

            app.MapDelete("/conversations/{userName}/{conversationId}", async (string userName, string conversationId, AppDbContext db) =>
            {
                var messages = db.ChatMessages
                    .Where(x => x.UserName == userName && x.ConversationId == conversationId)
                    .ToList();

                db.ChatMessages.RemoveRange(messages);
                await db.SaveChangesAsync();

                return Results.Ok(new { message = "Conversation deleted." });
            });
        }
    }
}
