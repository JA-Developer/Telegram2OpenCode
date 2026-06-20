using System.ComponentModel.DataAnnotations;

namespace Telegram2OpenCode.Models;

public class TelegramBot
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Username { get; set; }

    [Required, MaxLength(200)]
    public string Token { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? WelcomeMessage { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsRunning { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DeletedAt { get; set; }
}
