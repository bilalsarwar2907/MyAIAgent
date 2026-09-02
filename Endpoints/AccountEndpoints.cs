using MyAIAgent.Data;
using MyAIAgent.Models;

namespace MyAIAgent.Endpoints
{
    /// <summary>Health check + register/login. Thin — no business logic beyond validation.</summary>
    public static class AccountEndpoints
    {
        public static void MapAccountEndpoints(this WebApplication app)
        {
            app.MapGet("/health", () => new { status = "OK", time = DateTime.UtcNow });

            app.MapPost("/register", async (RegisterRequest request, AppDbContext db) =>
            {
                if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password))
                    return Results.BadRequest("Username and password are required.");

                var existingUser = db.Users.FirstOrDefault(x => x.UserName == request.UserName);
                if (existingUser != null)
                    return Results.BadRequest("Username already exists.");

                db.Users.Add(new User { UserName = request.UserName, Password = request.Password });
                await db.SaveChangesAsync();
                return Results.Ok("User registered successfully.");
            });

            app.MapPost("/login", async (LoginRequest request, AppDbContext db) =>
            {
                if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                    return Results.BadRequest("Username and password are required.");

                var user = db.Users.FirstOrDefault(
                    x => x.UserName == request.Username && x.Password == request.Password);

                if (user == null) return Results.Unauthorized();
                return Results.Ok(new { message = "Login successful.", userName = user.UserName });
            });
        }
    }
}
