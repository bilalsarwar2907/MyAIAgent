using Microsoft.EntityFrameworkCore;
using MyAIAgent.Models;

namespace MyAIAgent.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext
        (
            DbContextOptions<AppDbContext> options
        )
            : base(options)
        {
        }

        public DbSet<ChatMessage> ChatMessages { get; set; }
    }
}