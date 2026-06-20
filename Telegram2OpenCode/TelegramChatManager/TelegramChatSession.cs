using System.Collections.Generic;

namespace Telegram2OpenCode.TelegramChatManager;

public class TelegramChatSession
{
    public long ChatId { get; }
    public ChatState State { get; set; } = ChatState.InitialMenu;
    public int? AiAgentId { get; set; }
    public int? TelegramBotId { get; set; }
    public string? OpenCodeSessionId { get; set; }
    public List<string> PendingSessionIds { get; set; } = new();

    public TelegramChatSession(long chatId)
    {
        ChatId = chatId;
    }
}
