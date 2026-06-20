using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram2OpenCode.Repositories;
using Telegram2OpenCode.TelegramChatManager;

namespace Telegram2OpenCode.Services;

public sealed class BotService : IHostedService, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OpenCodeManager _openCode;
    private readonly VibeUtils _vibeUtils;
    private readonly ConcurrentDictionary<int, (TelegramBotClient Client, string Username)> _bots = new();
    private readonly CancellationTokenSource _cts = new();
    private Timer? _syncTimer;

    private static readonly ReceiverOptions ReceiverOptions = new()
    {
        AllowedUpdates = Array.Empty<UpdateType>()
    };

    public BotService(IServiceScopeFactory scopeFactory, OpenCodeManager openCode, VibeUtils vibeUtils)
    {
        _scopeFactory = scopeFactory;
        _openCode = openCode;
        _vibeUtils = vibeUtils;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await SyncBotsAsync(cancellationToken);

        _syncTimer = new Timer(async _ => await SyncBotsAsync(_cts.Token), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _syncTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        _cts.Cancel();
        return Task.CompletedTask;
    }

    private async Task SyncBotsAsync(CancellationToken cancellationToken = default)
    {
        using var scope = _scopeFactory.CreateScope();
        var repo = scope.ServiceProvider.GetRequiredService<ITelegramBotRepository>();

        var runningBots = await repo.GetAllRunningAsync();

        var activeIds = runningBots.Select(b => b.Id).ToHashSet();

        foreach (var bot in runningBots)
        {
            if (_bots.ContainsKey(bot.Id))
                continue;

            try
            {
                var client = new TelegramBotClient(bot.Token);
                var me = await client.GetMe(cancellationToken);
                var username = me.Username ?? string.Empty;
                client.StartReceiving(
                    updateHandler: (b, u, ct) => HandleUpdateAsync(b, u, ct, username),
                    errorHandler: HandlePollingErrorAsync,
                    receiverOptions: ReceiverOptions,
                    cancellationToken: _cts.Token
                );
                _bots[bot.Id] = (client, username);
                Console.WriteLine($"Bot '{bot.Name}' started.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error starting bot '{bot.Name}': {ex.Message}");
            }
        }

        foreach (var kvp in _bots)
        {
            if (activeIds.Contains(kvp.Key))
                continue;

            if (_bots.TryRemove(kvp.Key, out var client))
            {
                Console.WriteLine($"Bot ID {kvp.Key} stopped.");
            }
        }
    }

    private async Task<TelegramChatSession> GetOrCreateSessionAsync(IChatSessionRepository repo, long chatId)
    {
        var session = await repo.GetByIdAsync(chatId);
        if (session is null)
        {
            session = new TelegramChatSession(chatId);
            await repo.CreateAsync(session);
        }
        return session;
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken, string botUsername)
    {
        if (update.Message is not { Text: { } text } message)
            return;

        var chatId = message.Chat.Id;
        var chatType = message.Chat.Type;

        var cleaned = text.Trim().ToLowerInvariant();

        if (chatType is ChatType.Group or ChatType.Supergroup)
        {
            if (!text.Contains($"@{botUsername}", StringComparison.OrdinalIgnoreCase))
                return;

            cleaned = cleaned.Replace($"@{botUsername}", string.Empty, StringComparison.OrdinalIgnoreCase)
                .Trim();
        }

        await botClient.SendChatAction(chatId, ChatAction.Typing, cancellationToken: cancellationToken);

        using var scope = _scopeFactory.CreateScope();
        var sessionRepo = scope.ServiceProvider.GetRequiredService<IChatSessionRepository>();

        var session = await GetOrCreateSessionAsync(sessionRepo, chatId);

        if (session.State == ChatState.InitialMenu)
        {
            if (cleaned.StartsWith("/start", StringComparison.OrdinalIgnoreCase) || cleaned.StartsWith("/help", StringComparison.OrdinalIgnoreCase))
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "Welcome. Select an option:\n1. New Session\n2. Existing Sessions\n3. Open Folder",
                    cancellationToken: cancellationToken
                );
                return;
            }

            switch (cleaned)
            {
                case "1":
                    try
                    {
                        var sessionId = await _openCode.CreateSessionAsync(new CreateSessionRequest { title = $"Chat {chatId}" }, cancellationToken);
                        session.OpenCodeSessionId = sessionId;
                        await sessionRepo.UpdateAsync(session);
                    }
                    catch (Exception ex)
                    {
                        await botClient.SendMessage(
                            chatId: chatId,
                            text: $"Error creating session in OpenCode: {ex.Message}",
                            cancellationToken: cancellationToken
                        );
                        return;
                    }

                    session.State = ChatState.Chat;
                    await sessionRepo.UpdateAsync(session);

                    await botClient.SendMessage(
                        chatId: chatId,
                        text: "You've entered Chat mode. Send a message and I'll forward it to OpenCode.",
                        cancellationToken: cancellationToken
                    );
                    break;

                case "2":
                    List<SessionItem> sessions;
                    try
                    {
                        sessions = await _openCode.GetSessionsAsync(cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        await botClient.SendMessage(
                            chatId: chatId,
                            text: $"Error fetching sessions: {ex.Message}",
                            cancellationToken: cancellationToken
                        );
                        break;
                    }

                    if (sessions.Count == 0)
                    {
                        await botClient.SendMessage(
                            chatId: chatId,
                            text: "No sessions available. Use option 1 to create a new one.",
                            cancellationToken: cancellationToken
                        );
                        break;
                    }

                    session.State = ChatState.SelectingSession;
                    session.PendingSessionIds = sessions.Select(s => s.Id).ToList();
                    await sessionRepo.UpdateAsync(session);

                    var sb = new StringBuilder();
                    sb.AppendLine("Select a session:");
                    for (int i = 0; i < sessions.Count; i++)
                    {
                        var title = string.IsNullOrEmpty(sessions[i].Title) ? sessions[i].Id : sessions[i].Title;
                        sb.AppendLine($"{i + 1}. {title}");
                    }

                    await botClient.SendMessage(
                        chatId: chatId,
                        text: sb.ToString(),
                        cancellationToken: cancellationToken
                    );
                    break;

                case "3":
                    session.State = ChatState.AwaitingFolderDescription;
                    await sessionRepo.UpdateAsync(session);
                    await botClient.SendMessage(
                        chatId: chatId,
                        text: "Describe the folder you want to open (e.g. 'the downloads folder' or 'my project in documents'):",
                        cancellationToken: cancellationToken
                    );
                    break;
            }

            return;
        }

        if (session.State == ChatState.SelectingSession)
        {
            if (int.TryParse(text.Trim(), out var index) && index >= 1 && index <= session.PendingSessionIds.Count)
            {
                session.OpenCodeSessionId = session.PendingSessionIds[index - 1];
                session.PendingSessionIds.Clear();
                session.State = ChatState.Chat;
                await sessionRepo.UpdateAsync(session);

                await botClient.SendMessage(
                    chatId: chatId,
                    text: "Session selected. Send a message and I'll forward it to OpenCode.",
                    cancellationToken: cancellationToken
                );
            }
            else
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "Invalid option. Please select a number from the list.",
                    cancellationToken: cancellationToken
                );
            }
            return;
        }

        if (session.State == ChatState.AwaitingFolderDescription)
        {
            string? path;
            try
            {
                path = await _vibeUtils.ConvertPromptToPath(text, cancellationToken);
            }
            catch (Exception ex)
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: $"Error finding folder: {ex.Message}",
                    cancellationToken: cancellationToken
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
                    cancellationToken: cancellationToken
                );
                return;
            }

            try
            {
                var sessionId = await _openCode.CreateSessionAsync(new CreateSessionRequest { title = $"Folder: {path}", directory = path }, cancellationToken);
                session.OpenCodeSessionId = sessionId;
                session.State = ChatState.Chat;
                await sessionRepo.UpdateAsync(session);

                await botClient.SendMessage(
                    chatId: chatId,
                    text: $"Session created in folder:\n{path}\n\nSend a message and I'll forward it to OpenCode.",
                    cancellationToken: cancellationToken
                );
            }
            catch (Exception ex)
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: $"Error creating session in OpenCode: {ex.Message}",
                    cancellationToken: cancellationToken
                );
                session.State = ChatState.InitialMenu;
                await sessionRepo.UpdateAsync(session);
            }

            return;
        }

        if (session.State == ChatState.Chat)
        {
            if (session.OpenCodeSessionId is null)
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "No active OpenCode session.",
                    cancellationToken: cancellationToken
                );
                return;
            }

            try
            {
                var reply = await _openCode.SendMessageAsync(session.OpenCodeSessionId, text, cancellationToken)
                    ?? "No response from OpenCode.";

                await botClient.SendMessage(
                    chatId: chatId,
                    text: reply,
                    replyParameters: message.Id,
                    cancellationToken: cancellationToken
                );
            }
            catch (Exception ex)
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: $"Error communicating with OpenCode: {ex.Message}",
                    cancellationToken: cancellationToken
                );
            }
        }
    }

    private static Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Bot error: {exception.Message}");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _syncTimer?.Dispose();
        _cts.Cancel();
        _cts.Dispose();
    }
}
