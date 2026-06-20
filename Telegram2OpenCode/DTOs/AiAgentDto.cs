using System.ComponentModel.DataAnnotations;

namespace Telegram2OpenCode.DTOs;

public class AiAgentDto
{
    public int Id { get; set; }

    [Required(ErrorMessage = "Name is required")]
    [MaxLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
    public string Name { get; set; } = string.Empty;

    [MaxLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
    public string? Description { get; set; }

    public string? SystemPrompt { get; set; }

    [Required(ErrorMessage = "Model is required")]
    [MaxLength(50, ErrorMessage = "Model cannot exceed 50 characters")]
    public string Model { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? DeletedAt { get; set; }
}
