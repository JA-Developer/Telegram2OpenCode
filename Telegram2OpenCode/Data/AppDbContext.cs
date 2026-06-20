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
    public DbSet<ChatSessionEntity> ChatSessions => Set<ChatSessionEntity>();

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

        modelBuilder.Entity<ChatSessionEntity>(entity =>
        {
            entity.ToTable("ChatSessions");
            entity.HasKey(e => e.ChatId);
        });
    }
}
