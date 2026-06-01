using Microsoft.EntityFrameworkCore;
using Telegram2OpenCode.Data;
using Telegram2OpenCode.Models;

namespace Telegram2OpenCode.Repositories;

public class AiAgentRepository : IAiAgentRepository
{
    private readonly AppDbContext _db;

    public AiAgentRepository(AppDbContext db)
    {
        _db = db;
    }

    public async Task<List<AiAgent>> GetAllAsync()
    {
        return await _db.AiAgents.ToListAsync();
    }

    public async Task<AiAgent?> GetByIdAsync(int id)
    {
        return await _db.AiAgents.FirstOrDefaultAsync(e => e.Id == id);
    }

    public async Task<AiAgent> CreateAsync(AiAgent agent)
    {
        _db.AiAgents.Add(agent);
        await _db.SaveChangesAsync();
        return agent;
    }

    public async Task<AiAgent> UpdateAsync(AiAgent agent)
    {
        var existing = await _db.AiAgents.FirstOrDefaultAsync(e => e.Id == agent.Id)
            ?? throw new InvalidOperationException($"AiAgent with Id {agent.Id} not found or has been deleted.");

        existing.Name = agent.Name;
        existing.Description = agent.Description;
        existing.SystemPrompt = agent.SystemPrompt;
        existing.Model = agent.Model;
        existing.IsActive = agent.IsActive;

        await _db.SaveChangesAsync();
        return existing;
    }

    public async Task DeleteAsync(int id)
    {
        var agent = await _db.AiAgents.FirstOrDefaultAsync(e => e.Id == id);
        if (agent is not null)
        {
            agent.DeletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();
        }
    }
}
