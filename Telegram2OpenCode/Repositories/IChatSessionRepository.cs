using Telegram2OpenCode.TelegramChatManager;

namespace Telegram2OpenCode.Repositories;

public interface IChatSessionRepository
{
    Task<TelegramChatSession?> GetByIdAsync(long chatId);
    Task CreateAsync(TelegramChatSession session);
    Task UpdateAsync(TelegramChatSession session);
    Task DeleteAsync(long chatId);
}
