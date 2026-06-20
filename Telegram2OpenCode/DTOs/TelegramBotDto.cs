using System.ComponentModel.DataAnnotations;

namespace Telegram2OpenCode.DTOs;

public class TelegramBotDto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Name is required")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50, ErrorMessage = "Username cannot exceed 50 characters")]
    public string? Username { get; set; }

    [Required(ErrorMessage = "Token is required")]
    [MaxLength(200, ErrorMessage = "Token cannot exceed 200 characters")]
    public string Token { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "Welcome message cannot exceed 500 characters")]
    public string? WelcomeMessage { get; set; }

    public bool IsActive { get; set; }

    public bool IsRunning { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }
}
