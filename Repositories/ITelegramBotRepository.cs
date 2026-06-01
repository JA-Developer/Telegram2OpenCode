using Telegram2OpenCode.Models;

namespace Telegram2OpenCode.Repositories;

public interface ITelegramBotRepository
{
    Task<List<TelegramBot>> GetAllAsync();
    Task<List<TelegramBot>> GetAllRunningAsync();
    Task<TelegramBot?> GetByIdAsync(int id);
    Task<TelegramBot> CreateAsync(TelegramBot bot);
    Task<TelegramBot> UpdateAsync(TelegramBot bot);
    Task DeleteAsync(int id);
}
