using System.ComponentModel.DataAnnotations;

namespace Telegram2OpenCode.DTOs;

public class UpdateTelegramBotDto
{
    [Required(ErrorMessage = "El nombre es obligatorio")]
    [MaxLength(100, ErrorMessage = "El nombre no puede exceder 100 caracteres")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(50, ErrorMessage = "El username no puede exceder 50 caracteres")]
    public string? Username { get; set; }

    [Required(ErrorMessage = "El token es obligatorio")]
    [MaxLength(200, ErrorMessage = "El token no puede exceder 200 caracteres")]
    public string Token { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "El mensaje de bienvenida no puede exceder 500 caracteres")]
    public string? WelcomeMessage { get; set; }

    public bool IsActive { get; set; }

    public bool IsRunning { get; set; }
}
