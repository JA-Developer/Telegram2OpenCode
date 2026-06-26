using Telegram2OpenCode.TelegramChatManager;

namespace Telegram2OpenCode.Services.Handlers;

public sealed class StateHandlerResolver
{
    private readonly Dictionary<ChatState, IStateHandler> _handlers;

    public StateHandlerResolver(IEnumerable<IStateHandler> handlers)
    {
        _handlers = handlers.ToDictionary(h => h.State);
    }

    public IStateHandler GetHandler(ChatState state)
    {
        return _handlers.GetValueOrDefault(state)
            ?? throw new InvalidOperationException($"No handler registered for state {state}");
    }
}
