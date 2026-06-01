using Telegram2OpenCode.DTOs;
using Telegram2OpenCode.Models;

namespace Telegram2OpenCode.DTOs.Mapping;

public static class TelegramBotMapper
{
    public static TelegramBotDto ToDto(this TelegramBot entity)
    {
        return new TelegramBotDto
        {
            Id = entity.Id,
            Name = entity.Name,
            Username = entity.Username,
            Token = entity.Token,
            WelcomeMessage = entity.WelcomeMessage,
            IsActive = entity.IsActive,
            IsRunning = entity.IsRunning,
            CreatedAt = entity.CreatedAt,
            DeletedAt = entity.DeletedAt
        };
    }

    public static TelegramBot ToEntity(this CreateTelegramBotDto dto)
    {
        return new TelegramBot
        {
            Name = dto.Name,
            Username = dto.Username,
            Token = dto.Token,
            WelcomeMessage = dto.WelcomeMessage,
            IsRunning = dto.IsRunning
        };
    }

    public static void ApplyUpdate(this TelegramBot entity, UpdateTelegramBotDto dto)
    {
        entity.Name = dto.Name;
        entity.Username = dto.Username;
        entity.Token = dto.Token;
        entity.WelcomeMessage = dto.WelcomeMessage;
        entity.IsActive = dto.IsActive;
        entity.IsRunning = dto.IsRunning;
    }
}
