using Telegram2OpenCode.Models;

namespace Telegram2OpenCode.Repositories;

public interface IAiAgentRepository
{
    Task<List<AiAgent>> GetAllAsync();
    Task<AiAgent?> GetByIdAsync(int id);
    Task<AiAgent> CreateAsync(AiAgent agent);
    Task<AiAgent> UpdateAsync(AiAgent agent);
    Task DeleteAsync(int id);
}
