using System.ComponentModel.DataAnnotations;

namespace Telegram2OpenCode.DTOs;

public class UpdateAiAgentDto
{
    [Required(ErrorMessage = "El nombre es obligatorio")]
    [MaxLength(100, ErrorMessage = "El nombre no puede exceder 100 caracteres")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "La descripción no puede exceder 500 caracteres")]
    public string? Description { get; set; }

    public string? SystemPrompt { get; set; }

    [Required(ErrorMessage = "El modelo es obligatorio")]
    [MaxLength(50, ErrorMessage = "El modelo no puede exceder 50 caracteres")]
    public string Model { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}
