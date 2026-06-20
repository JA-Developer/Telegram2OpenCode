using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Telegram2OpenCode.Data;
using Telegram2OpenCode.Models;
using Telegram2OpenCode.TelegramChatManager;

namespace Telegram2OpenCode.Repositories;

public class ChatSessionRepository : IChatSessionRepository
{
    private readonly AppDbContext _db;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        IncludeFields = false
    };

    public ChatSessionRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<TelegramChatSession?> GetByIdAsync(long chatId)
    {
        var entity = await _db.ChatSessions.FirstOrDefaultAsync(e => e.ChatId == chatId);
        if (entity is null)
            return null;

        return JsonSerializer.Deserialize<TelegramChatSession>(entity.StateJson, JsonOptions);
    }

    public async Task CreateAsync(TelegramChatSession session)
    {
        var entity = new ChatSessionEntity
        {
            ChatId = session.ChatId,
            StateJson = JsonSerializer.Serialize(session, JsonOptions),
            UpdatedAt = DateTime.UtcNow
        };
        _db.ChatSessions.Add(entity);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateAsync(TelegramChatSession session)
    {
        var entity = await _db.ChatSessions.FirstOrDefaultAsync(e => e.ChatId == session.ChatId)
            ?? throw new InvalidOperationException($"ChatSession with ChatId {session.ChatId} not found.");
        entity.StateJson = JsonSerializer.Serialize(session, JsonOptions);
        entity.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(long chatId)
    {
        var entity = await _db.ChatSessions.FirstOrDefaultAsync(e => e.ChatId == chatId);
        if (entity is not null)
        {
            _db.ChatSessions.Remove(entity);
            await _db.SaveChangesAsync();
        }
    }
}
