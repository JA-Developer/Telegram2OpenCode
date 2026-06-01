using System;
using System.Collections.Concurrent;
using System.Linq;
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
    private readonly ConcurrentDictionary<int, TelegramBotClient> _bots = new();
    private readonly ConcurrentDictionary<long, TelegramChatSession> _sessions = new();
    private readonly CancellationTokenSource _cts = new();
    private Timer? _syncTimer;

    private static readonly ReceiverOptions ReceiverOptions = new()
    {
        AllowedUpdates = Array.Empty<UpdateType>()
    };

    public BotService(IServiceScopeFactory scopeFactory, OpenCodeManager openCode)
    {
        _scopeFactory = scopeFactory;
        _openCode = openCode;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await SyncBotsAsync();

        _syncTimer = new Timer(async _ => await SyncBotsAsync(), null, TimeSpan.FromSeconds(30), TimeSpan.FromSeconds(30));
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _syncTimer?.Change(Timeout.Infinite, Timeout.Infinite);
        _cts.Cancel();
        return Task.CompletedTask;
    }

    private async Task SyncBotsAsync()
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
                client.StartReceiving(
                    updateHandler: HandleUpdateAsync,
                    errorHandler: HandlePollingErrorAsync,
                    receiverOptions: ReceiverOptions,
                    cancellationToken: _cts.Token
                );
                _bots[bot.Id] = client;
                Console.WriteLine($"Bot '{bot.Name}' iniciado.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al iniciar bot '{bot.Name}': {ex.Message}");
            }
        }

        foreach (var kvp in _bots)
        {
            if (activeIds.Contains(kvp.Key))
                continue;

            if (_bots.TryRemove(kvp.Key, out var client))
            {
                Console.WriteLine($"Bot ID {kvp.Key} detenido.");
            }
        }
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Message is not { Text: { } messageText } message)
            return;

        var chatId = message.Chat.Id;

        var session = _sessions.GetOrAdd(chatId, new TelegramChatSession(chatId));

        if (session.State == ChatState.InitialMenu)
        {
            if (messageText.StartsWith("/start") || messageText.StartsWith("/help"))
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "Bienvenido. Selecciona una opción:\n1. Iniciar modo Chat",
                    cancellationToken: cancellationToken
                );
                return;
            }

            var cleaned = messageText.Trim().ToLowerInvariant();

            if (cleaned == "1")
            {
                try
                {
                    var sessionId = await _openCode.CreateSessionAsync($"Chat {chatId}", cancellationToken);
                    session.OpenCodeSessionId = sessionId;
                }
                catch (Exception ex)
                {
                    await botClient.SendMessage(
                        chatId: chatId,
                        text: $"Error al crear sesión en OpenCode: {ex.Message}",
                        cancellationToken: cancellationToken
                    );
                    return;
                }

                session.State = ChatState.Chat;

                await botClient.SendMessage(
                    chatId: chatId,
                    text: "Has entrado en modo Chat. Escribe algo y lo reenviaré a OpenCode.",
                    cancellationToken: cancellationToken
                );
            }

            return;
        }

        if (session.State == ChatState.Chat)
        {
            if (session.OpenCodeSessionId is null)
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "No hay sesión de OpenCode activa.",
                    cancellationToken: cancellationToken
                );
                return;
            }

            try
            {
                var reply = await _openCode.SendMessageAsync(session.OpenCodeSessionId, messageText, cancellationToken)
                    ?? "Sin respuesta de OpenCode.";

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
                    text: $"Error al comunicar con OpenCode: {ex.Message}",
                    cancellationToken: cancellationToken
                );
            }
        }
    }

    private static Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        Console.WriteLine($"Error en el bot: {exception.Message}");
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _syncTimer?.Dispose();
        _cts.Cancel();
        _cts.Dispose();
    }
}
