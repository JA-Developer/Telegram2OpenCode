namespace Telegram2OpenCode.Models;

public class ChatSessionEntity
{
    public long ChatId { get; set; }
    public string StateJson { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
