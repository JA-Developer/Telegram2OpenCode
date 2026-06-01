using Microsoft.EntityFrameworkCore;
using Telegram2OpenCode.Models;

namespace Telegram2OpenCode.Data;

public class AppDbContext : DbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<AiAgent> AiAgents => Set<AiAgent>();
    public DbSet<TelegramBot> TelegramBots => Set<TelegramBot>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<AiAgent>(entity =>
        {
            entity.ToTable("AiAgents");
            entity.HasQueryFilter(e => e.DeletedAt == null);
        });

        modelBuilder.Entity<TelegramBot>(entity =>
        {
            entity.ToTable("TelegramBots");
            entity.HasQueryFilter(e => e.DeletedAt == null);
        });
    }
}
