using Telegram2OpenCode.Repositories;
using Telegram2OpenCode.TelegramChatManager;

namespace Telegram2OpenCode.Services;

public sealed class ChatSessionService
{
    // TODO: Add method to check if a session exists before creating
    public async Task<TelegramChatSession> GetOrCreateAsync(IChatSessionRepository repo, long chatId)
    {
        var session = await repo.GetByIdAsync(chatId);
        if (session is null)
        {
            session = new TelegramChatSession(chatId);
            await repo.CreateAsync(session);
        }
        return session;
    }

    public async Task TransitionToStateAsync(IChatSessionRepository repo, TelegramChatSession session, ChatState newState)
    {
        session.State = newState;
        await repo.UpdateAsync(session);
    }
}
