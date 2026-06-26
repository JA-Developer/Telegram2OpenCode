using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram2OpenCode.Repositories;
using Telegram2OpenCode.TelegramChatManager;

namespace Telegram2OpenCode.Services.Handlers;

public sealed class SelectingSessionHandler : IStateHandler
{
    public ChatState State => ChatState.SelectingSession;

    public async Task HandleAsync(ITelegramBotClient botClient, Message message, TelegramChatSession session, IChatSessionRepository sessionRepo, string cleanedText, CancellationToken ct)
    {
        var chatId = message.Chat.Id;

        if (int.TryParse(cleanedText, out var index) && index >= 1 && index <= session.PendingSessionIds.Count)
        {
            session.OpenCodeSessionId = session.PendingSessionIds[index - 1];
            session.PendingSessionIds.Clear();
            session.State = ChatState.Chat;
            await sessionRepo.UpdateAsync(session);

            await botClient.SendMessage(
                chatId: chatId,
                text: "Session selected. Send a message and I'll forward it to OpenCode.",
                cancellationToken: ct
            );
        }
        else
        {
            await botClient.SendMessage(
                chatId: chatId,
                text: "Invalid option. Please select a number from the list.",
                cancellationToken: ct
            );
        }
    }
}
