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

        public DbSet<User> Users { get; set; } 

        public DbSet<WatchlistItem> WatchlistItems { get; set; }

        public DbSet<PortfolioItem> PortfolioItems { get; set; }

        public DbSet<PriceAlert> PriceAlerts { get; set; }

        public DbSet<PaperTrade> PaperTrades { get; set; }
    }
}