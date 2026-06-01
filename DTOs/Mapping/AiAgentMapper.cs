using Telegram2OpenCode.DTOs;
using Telegram2OpenCode.Models;

namespace Telegram2OpenCode.DTOs.Mapping;

public static class AiAgentMapper
{
    public static AiAgentDto ToDto(this AiAgent entity)
    {
        return new AiAgentDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Description = entity.Description,
            SystemPrompt = entity.SystemPrompt,
            Model = entity.Model,
            IsActive = entity.IsActive,
            CreatedAt = entity.CreatedAt,
            DeletedAt = entity.DeletedAt
        };
    }

    public static AiAgent ToEntity(this CreateAiAgentDto dto)
    {
        return new AiAgent
        {
            Name = dto.Name,
            Description = dto.Description,
            SystemPrompt = dto.SystemPrompt,
            Model = dto.Model
        };
    }

    public static void ApplyUpdate(this AiAgent entity, UpdateAiAgentDto dto)
    {
        entity.Name = dto.Name;
        entity.Description = dto.Description;
        entity.SystemPrompt = dto.SystemPrompt;
        entity.Model = dto.Model;
        entity.IsActive = dto.IsActive;
    }
}
