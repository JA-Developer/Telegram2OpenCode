using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram2OpenCode.Repositories;
using Telegram2OpenCode.TelegramChatManager;

namespace Telegram2OpenCode.Services.Handlers;

public sealed class InitialMenuHandler : IStateHandler
{
    private readonly OpenCodeManager _openCode;
    private readonly ChatSessionService _chatSession;

    public ChatState State => ChatState.InitialMenu;

    public InitialMenuHandler(OpenCodeManager openCode, ChatSessionService chatSession)
    {
        _openCode = openCode;
        _chatSession = chatSession;
    }

    public async Task HandleAsync(ITelegramBotClient botClient, Message message, TelegramChatSession session, IChatSessionRepository sessionRepo, string cleanedText, CancellationToken ct)
    {
        var chatId = message.Chat.Id;

        if (cleanedText.StartsWith("/start", StringComparison.OrdinalIgnoreCase) ||
            cleanedText.StartsWith("/help", StringComparison.OrdinalIgnoreCase))
        {
            await botClient.SendMessage(
                chatId: chatId,
                text: "Welcome. Select an option:\n1. New Session\n2. Existing Sessions\n3. Open Folder",
                cancellationToken: ct
            );
            return;
        }

        switch (cleanedText)
        {
            case "1":
                await HandleNewSession(botClient, session, sessionRepo, chatId, ct);
                break;

            case "2":
                await HandleListSessions(botClient, session, sessionRepo, chatId, ct);
                break;

            case "3":
                session.State = ChatState.AwaitingFolderDescription;
                await sessionRepo.UpdateAsync(session);
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "Describe the folder you want to open (e.g. 'the downloads folder' or 'my project in documents'):",
                    cancellationToken: ct
                );
                break;
        }
    }

    private async Task HandleNewSession(ITelegramBotClient botClient, TelegramChatSession session, IChatSessionRepository sessionRepo, long chatId, CancellationToken ct)
    {
        try
        {
            var sessionId = await _openCode.CreateSessionAsync(new CreateSessionRequest { title = $"Chat {chatId}" }, ct);
            session.OpenCodeSessionId = sessionId;
            await sessionRepo.UpdateAsync(session);
        }
        catch (Exception ex)
        {
            await botClient.SendMessage(
                chatId: chatId,
                text: $"Error creating session in OpenCode: {ex.Message}",
                cancellationToken: ct
            );
            return;
        }

        session.State = ChatState.Chat;
        await sessionRepo.UpdateAsync(session);

        await botClient.SendMessage(
            chatId: chatId,
            text: "You've entered Chat mode. Send a message and I'll forward it to OpenCode.",
            cancellationToken: ct
        );
    }

    private async Task HandleListSessions(ITelegramBotClient botClient, TelegramChatSession session, IChatSessionRepository sessionRepo, long chatId, CancellationToken ct)
    {
        List<SessionItem> sessions;
        try
        {
            sessions = await _openCode.GetSessionsAsync(ct);
        }
        catch (Exception ex)
        {
            await botClient.SendMessage(
                chatId: chatId,
                text: $"Error fetching sessions: {ex.Message}",
                cancellationToken: ct
            );
            return;
        }

        if (sessions.Count == 0)
        {
            await botClient.SendMessage(
                chatId: chatId,
                text: "No sessions available. Use option 1 to create a new one.",
                cancellationToken: ct
            );
            return;
        }

        session.State = ChatState.SelectingSession;
        session.PendingSessionIds = sessions.Select(s => s.Id).ToList();
        await sessionRepo.UpdateAsync(session);

        var sb = new System.Text.StringBuilder();
        sb.AppendLine("Select a session:");
        for (int i = 0; i < sessions.Count; i++)
        {
            var title = string.IsNullOrEmpty(sessions[i].Title) ? sessions[i].Id : sessions[i].Title;
            sb.AppendLine($"{i + 1}. {title}");
        }

        await botClient.SendMessage(
            chatId: chatId,
            text: sb.ToString(),
            cancellationToken: ct
        );
    }
}
