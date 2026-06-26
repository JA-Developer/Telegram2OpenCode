using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram2OpenCode.Repositories;
using Telegram2OpenCode.TelegramChatManager;

namespace Telegram2OpenCode.Services.Handlers;

public sealed class AwaitingFolderHandler : IStateHandler
{
    private readonly OpenCodeManager _openCode;
    private readonly VibeUtils _vibeUtils;
    private readonly ChatSessionService _chatSession;

    public ChatState State => ChatState.AwaitingFolderDescription;

    public AwaitingFolderHandler(OpenCodeManager openCode, VibeUtils vibeUtils, ChatSessionService chatSession)
    {
        _openCode = openCode;
        _vibeUtils = vibeUtils;
        _chatSession = chatSession;
    }

    public async Task HandleAsync(ITelegramBotClient botClient, Message message, TelegramChatSession session, IChatSessionRepository sessionRepo, string cleanedText, CancellationToken ct)
    {
        var chatId = message.Chat.Id;

        string? path;
        try
        {
            path = await _vibeUtils.ConvertPromptToPath(
                cleanedText,
                async _ => await botClient.SendChatAction(chatId, ChatAction.Typing, cancellationToken: ct),
                ct);
        }
        catch (Exception ex)
        {
            await botClient.SendMessage(
                chatId: chatId,
                text: $"Error finding folder: {ex.Message}",
                cancellationToken: ct
            );
            session.State = ChatState.InitialMenu;
            await sessionRepo.UpdateAsync(session);
            return;
        }

        if (path is null)
        {
            await botClient.SendMessage(
                chatId: chatId,
                text: "Could not identify a folder. Try describing it differently or type *cancel* to go back to the menu.",
                cancellationToken: ct
            );
            return;
        }

        try
        {
            var sessionId = await _openCode.CreateSessionAsync(new CreateSessionRequest { title = $"Folder: {path}", directory = path }, ct);
            session.OpenCodeSessionId = sessionId;
            session.State = ChatState.Chat;
            await sessionRepo.UpdateAsync(session);

            await botClient.SendMessage(
                chatId: chatId,
                text: $"Session created in folder:\n{path}\n\nSend a message and I'll forward it to OpenCode.",
                cancellationToken: ct
            );
        }
        catch (Exception ex)
        {
            await botClient.SendMessage(
                chatId: chatId,
                text: $"Error creating session in OpenCode: {ex.Message}",
                cancellationToken: ct
            );
            session.State = ChatState.InitialMenu;
            await sessionRepo.UpdateAsync(session);
        }
    }
}
