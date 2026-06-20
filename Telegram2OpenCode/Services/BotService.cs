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
    private readonly ConcurrentDictionary<int, TelegramBotClient> _bots = new();
    private readonly ConcurrentDictionary<long, TelegramChatSession> _sessions = new();
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
                    text: "Bienvenido. Selecciona una opción:\n1. Nueva Sesión\n2. Sesiones Existentes\n3. Abrir Folder",
                    cancellationToken: cancellationToken
                );
                return;
            }

            var cleaned = messageText.Trim().ToLowerInvariant();

            switch (cleaned)
            {
                case "1":
                    try
                    {
                        var sessionId = await _openCode.CreateSessionAsync(new CreateSessionRequest { title = $"Chat {chatId}" }, cancellationToken);
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
                            text: $"Error al obtener sesiones: {ex.Message}",
                            cancellationToken: cancellationToken
                        );
                        break;
                    }

                    if (sessions.Count == 0)
                    {
                        await botClient.SendMessage(
                            chatId: chatId,
                            text: "No hay sesiones disponibles. Usa la opción 1 para crear una nueva.",
                            cancellationToken: cancellationToken
                        );
                        break;
                    }

                    session.State = ChatState.SelectingSession;
                    session.PendingSessionIds = sessions.Select(s => s.Id).ToList();

                    var sb = new StringBuilder();
                    sb.AppendLine("Selecciona una sesión:");
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
                    await botClient.SendMessage(
                        chatId: chatId,
                        text: "Describe la carpeta que quieres abrir (ej: 'la carpeta de descargas' o 'mi proyecto en documentos'):",
                        cancellationToken: cancellationToken
                    );
                    break;
            }

            return;
        }

        if (session.State == ChatState.SelectingSession)
        {
            if (int.TryParse(messageText.Trim(), out var index) && index >= 1 && index <= session.PendingSessionIds.Count)
            {
                session.OpenCodeSessionId = session.PendingSessionIds[index - 1];
                session.PendingSessionIds.Clear();
                session.State = ChatState.Chat;

                await botClient.SendMessage(
                    chatId: chatId,
                    text: "Sesión seleccionada. Escribe algo y lo reenviaré a OpenCode.",
                    cancellationToken: cancellationToken
                );
            }
            else
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "Opción inválida. Por favor, selecciona un número de la lista.",
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
                path = await _vibeUtils.ConvertPromptToPath(messageText, cancellationToken);
            }
            catch (Exception ex)
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: $"Error al buscar la carpeta: {ex.Message}",
                    cancellationToken: cancellationToken
                );
                session.State = ChatState.InitialMenu;
                return;
            }

            if (path is null)
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "No pude identificar una carpeta. Intenta describirla de otra forma o escribe *cancelar* para volver al menú.",
                    cancellationToken: cancellationToken
                );
                return;
            }

            try
            {
                var sessionId = await _openCode.CreateSessionAsync(new CreateSessionRequest { title = $"Folder: {path}", directory = path }, cancellationToken);
                session.OpenCodeSessionId = sessionId;
                session.State = ChatState.Chat;

                await botClient.SendMessage(
                    chatId: chatId,
                    text: $"Sesión creada en la carpeta:\n{path}\n\nEscribe algo y lo reenviaré a OpenCode.",
                    cancellationToken: cancellationToken
                );
            }
            catch (Exception ex)
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: $"Error al crear sesión en OpenCode: {ex.Message}",
                    cancellationToken: cancellationToken
                );
                session.State = ChatState.InitialMenu;
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
