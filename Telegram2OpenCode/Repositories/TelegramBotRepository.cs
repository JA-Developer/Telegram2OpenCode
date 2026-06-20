using Microsoft.EntityFrameworkCore;
using Telegram2OpenCode.Data;
using Telegram2OpenCode.Models;

namespace Telegram2OpenCode.Repositories;

public class TelegramBotRepository : ITelegramBotRepository
{
    private readonly AppDbContext _db;

    public TelegramBotRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<TelegramBot>> GetAllAsync()
    {
        return await _db.TelegramBots.ToListAsync();
    }

    public async Task<List<TelegramBot>> GetAllRunningAsync()
    {
        return await _db.TelegramBots.Where(b => b.IsRunning).ToListAsync();
    }

    public async Task<TelegramBot?> GetByIdAsync(int id)
    {
        return await _db.TelegramBots.FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<TelegramBot> CreateAsync(TelegramBot bot)
    {
        _db.TelegramBots.Add(bot);
        await _db.SaveChangesAsync();
        return bot;
    }

    public async Task<TelegramBot> UpdateAsync(TelegramBot bot)
    {
        var existing = await _db.TelegramBots.FirstOrDefaultAsync(e => e.Id == bot.Id)
            ?? throw new InvalidOperationException($"TelegramBot with Id {bot.Id} not found or has been deleted.");

        existing.Name = bot.Name;
        existing.Username = bot.Username;
        existing.Token = bot.Token;
        existing.WelcomeMessage = bot.WelcomeMessage;
        existing.IsActive = bot.IsActive;
        existing.IsRunning = bot.IsRunning;

        await _db.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(int id)
    {
        var bot = await _db.TelegramBots.FirstOrDefaultAsync(e => e.Id == id);
        if (bot is not null)
        {
            bot.DeletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }
}
