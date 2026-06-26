using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram2OpenCode.Repositories;
using Telegram2OpenCode.TelegramChatManager;

namespace Telegram2OpenCode.Services.Handlers;

public sealed class ChatHandler : IStateHandler
{
    private readonly OpenCodeManager _openCode;

    public ChatState State => ChatState.Chat;

    public ChatHandler(OpenCodeManager openCode)
    {
        _openCode = openCode;
    }

    public async Task HandleAsync(ITelegramBotClient botClient, Message message, TelegramChatSession session, IChatSessionRepository sessionRepo, string cleanedText, CancellationToken ct)
    {
        var chatId = message.Chat.Id;

        if (session.OpenCodeSessionId is null)
        {
            await botClient.SendMessage(
                chatId: chatId,
                text: "No active OpenCode session.",
                cancellationToken: ct
            );
            return;
        }

        try
        {
            var reply = await _openCode.SendMessageAsync(
                    session.OpenCodeSessionId,
                    cleanedText,
                    async _ => await botClient.SendChatAction(chatId, ChatAction.Typing, cancellationToken: ct),
                    ct)
                ?? "No response from OpenCode.";

            await botClient.SendMessage(
                chatId: chatId,
                text: reply,
                replyParameters: message.Id,
                cancellationToken: ct
            );
        }
        catch (Exception ex)
        {
            await botClient.SendMessage(
                chatId: chatId,
                text: $"Error communicating with OpenCode: {ex.Message}",
                cancellationToken: ct
            );
        }
    }
}
