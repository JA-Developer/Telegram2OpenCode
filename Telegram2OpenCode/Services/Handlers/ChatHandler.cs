using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram2OpenCode.Repositories;
using Telegram2OpenCode.TelegramChatManager;

namespace Telegram2OpenCode.Services.Handlers;

public sealed class ChatHandler : IStateHandler
{
    private readonly OpenCodeManager _openCode;
    private readonly DiffSummaryBuilder _diffSummary;

    public ChatState State => ChatState.Chat;

    public ChatHandler(OpenCodeManager openCode, DiffSummaryBuilder diffSummary)
    {
        _openCode = openCode;
        _diffSummary = diffSummary;
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
            var editEvents = new List<EditEvent>();

            var reply = await _openCode.SendMessageAsync(
                    session.OpenCodeSessionId,
                    cleanedText,
                    async line =>
                    {
                        await botClient.SendChatAction(chatId, ChatAction.Typing, cancellationToken: ct);
                        if (OpenCodeEventParser.TryParseEditEvent(line, out var edit))
                            editEvents.Add(edit!);
                    },
                    ct)
                ?? "No response from OpenCode.";

            var diffText = _diffSummary.Build(editEvents);
            var finalText = diffText is null ? reply : $"{reply}\n\n{diffText}";

            await botClient.SendMessage(
                chatId: chatId,
                text: finalText,
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
