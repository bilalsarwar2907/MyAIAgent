using MyAIAgent.Data;
using MyAIAgent.Models;
using MyAIAgent.Services;

namespace MyAIAgent.Endpoints
{
    /// <summary>Health check + register/login. Passwords are BCrypt-hashed;
    /// legacy plaintext rows are upgraded transparently on next successful login.</summary>
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

                db.Users.Add(new User
                {
                    UserName = request.UserName,
                    Password = PasswordHasher.Hash(request.Password)
                });
                await db.SaveChangesAsync();
                return Results.Ok("User registered successfully.");
            });

            app.MapPost("/login", async (LoginRequest request, AppDbContext db) =>
            {
                if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
                    return Results.BadRequest("Username and password are required.");

                var user = db.Users.FirstOrDefault(x => x.UserName == request.Username);
                if (user == null) return Results.Unauthorized();

                if (!PasswordHasher.Verify(request.Password, user.Password, out var needsRehash))
                    return Results.Unauthorized();

                if (needsRehash)
                {
                    user.Password = PasswordHasher.Hash(request.Password);
                    await db.SaveChangesAsync();
                }

                return Results.Ok(new { message = "Login successful.", userName = user.UserName });
            });
        }
    }
}
