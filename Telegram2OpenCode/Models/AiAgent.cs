using System.ComponentModel.DataAnnotations;

namespace Telegram2OpenCode.Models;

public class AiAgent
{
    public int Id { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500)]
    public string? Description { get; set; }

    public string? SystemPrompt { get; set; }

    [Required, MaxLength(50)]
    public string Model { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? DeletedAt { get; set; }
}
