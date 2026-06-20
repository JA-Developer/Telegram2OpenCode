using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Telegram2OpenCode.TelegramChatManager;

public class TelegramChatSession
{
    public long ChatId { get; set; }
    public ChatState State { get; set; } = ChatState.InitialMenu;
    public int? AiAgentId { get; set; }
    public int? TelegramBotId { get; set; }
    public string? OpenCodeSessionId { get; set; }
    public List<string> PendingSessionIds { get; set; } = new();

    [JsonConstructor]
    public TelegramChatSession() { }

    public TelegramChatSession(long chatId)
    {
        ChatId = chatId;
    }
}
