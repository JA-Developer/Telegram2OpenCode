using System.Collections.Concurrent;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram2OpenCode.Repositories;
using Telegram2OpenCode.Services.Handlers;
using Telegram2OpenCode.TelegramChatManager;

namespace Telegram2OpenCode.Services;

public sealed class BotService : IHostedService, IDisposable
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly StateHandlerResolver _handlerResolver;
    private readonly ChatSessionService _chatSession;
    private readonly ConcurrentDictionary<int, (TelegramBotClient Client, string Username)> _bots = new();
    private readonly CancellationTokenSource _cts = new();
    private Timer? _syncTimer;

    private static readonly ReceiverOptions ReceiverOptions = new()
    {
        AllowedUpdates = Array.Empty<UpdateType>()
    };

    public BotService(IServiceScopeFactory scopeFactory, StateHandlerResolver handlerResolver, ChatSessionService chatSession)
    {
        _scopeFactory = scopeFactory;
        _handlerResolver = handlerResolver;
        _chatSession = chatSession;
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

            if (_bots.TryRemove(kvp.Key, out _))
            {
                Console.WriteLine($"Bot ID {kvp.Key} stopped.");
            }
        }
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken, string botUsername)
    {
        if (update.Message is not { Text: { } text } message)
            return;

        var chatId = message.Chat.Id;
        var cleaned = text.Trim().ToLowerInvariant();

        if (message.Chat.Type is ChatType.Group or ChatType.Supergroup)
        {
            if (!text.Contains($"@{botUsername}", StringComparison.OrdinalIgnoreCase))
                return;

            cleaned = cleaned.Replace($"@{botUsername}", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
        }

        using var scope = _scopeFactory.CreateScope();
        var sessionRepo = scope.ServiceProvider.GetRequiredService<IChatSessionRepository>();

        var session = await _chatSession.GetOrCreateAsync(sessionRepo, chatId);
        var handler = _handlerResolver.GetHandler(session.State);
        await handler.HandleAsync(botClient, message, session, sessionRepo, cleaned, cancellationToken);
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
