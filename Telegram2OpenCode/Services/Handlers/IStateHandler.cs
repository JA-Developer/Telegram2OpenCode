using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram2OpenCode.Repositories;
using Telegram2OpenCode.TelegramChatManager;

namespace Telegram2OpenCode.Services.Handlers;

public interface IStateHandler
{
    ChatState State { get; }
    Task HandleAsync(ITelegramBotClient botClient, Message message, TelegramChatSession session, IChatSessionRepository sessionRepo, string cleanedText, CancellationToken ct);
}
