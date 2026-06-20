# Telegram2OpenCode

A Blazor web application that bridges Telegram bots with the [OpenCode](https://opencode.ai) AI agent platform. Manage your Telegram bots and AI agents through a web UI, while the bot service automatically creates OpenCode sessions and forwards messages between Telegram and OpenCode.

## Features

- **Multi-bot support** — Run multiple Telegram bots simultaneously, each with its own token
- **Automatic sync** — Bots marked as `Running` in the database are automatically started; removing the flag stops them
- **OpenCode integration** — Each chat session creates an OpenCode conversation and forwards messages
- **CRUD management** — Web UI to create, edit, and delete AI agents and Telegram bots
- **Soft delete** — All entities support soft deletion with `DeletedAt`
- **SQLite database** — Lightweight, zero-configuration storage

## Architecture

```
Telegram2OpenCode/
├── Components/          # Blazor UI components
│   ├── Pages/
│   │   ├── AiAgent/     # CRUD pages for AI agents
│   │   └── TelegramBot/ # CRUD pages for Telegram bots
│   └── Layout/          # Main layout and navigation
├── Data/                # EF Core DbContext
├── DTOs/                # Data transfer objects with validation
│   └── Mapping/         # Entity ↔ DTO mapping
├── Migrations/          # EF Core migrations
├── Models/              # Entity models
├── Repositories/        # Data access layer
├── Services/
│   ├── BotService.cs    # Singleton hosted service managing bot clients
│   └── OpenCodeManager.cs  # OpenCode API client
└── TelegramChatManager/ # Chat session state machine
    ├── ChatState.cs     # Finite state machine enum
    └── TelegramChatSession.cs
```

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
- A [Telegram Bot Token](https://t.me/BotFather) from BotFather
- (Optional) An [OpenCode](https://opencode.ai) instance

## Getting Started

1. **Clone the repository**

   ```bash
   git clone https://github.com/yourusername/Telegram2OpenCode.git
   cd Telegram2OpenCode/Telegram2OpenCode
   ```

2. **Configure the database**

   Update `appsettings.json` with your connection string (SQLite by default):

   ```json
   {
     "ConnectionStrings": {
       "DefaultConnection": "Data Source=Telegram2OpenCode.db"
     }
   }
   ```

3. **Configure OpenCode API URL**

   ```json
   {
     "OpenCode": {
       "ApiUrl": "http://localhost:4096"
     }
   }
   ```

4. **Apply migrations**

   ```bash
   dotnet ef database update
   ```

5. **Run the application**

   ```bash
   dotnet run
   ```

6. **Add a Telegram bot**

   - Open the web UI at `https://localhost:5001`
   - Navigate to **Telegram Bots** → **Create New**
   - Enter your bot's name, username, and token from BotFather
   - Check **Running** to start the bot immediately

7. **Chat with your bot**

   - Open Telegram and message your bot
   - Send `1` to enter Chat mode
   - The bot creates an OpenCode session and forwards your messages

## API Endpoints (OpenCode)

The service communicates with OpenCode via:

| Method | Endpoint                        | Description                |
|--------|---------------------------------|----------------------------|
| POST   | `/session`                      | Create a new conversation  |
| POST   | `/session/{id}/message`         | Send message and get reply |

## Configuration

All settings are in `appsettings.json`:

| Key                    | Description                  | Default                  |
|------------------------|------------------------------|--------------------------|
| `ConnectionStrings:DefaultConnection` | SQLite database path | `Data Source=Telegram2OpenCode.db` |
| `OpenCode:ApiUrl`      | OpenCode API base URL        | `http://localhost:5000`  |

## Stack

- **Frontend**: Blazor Server with Interactive Server rendering
- **Backend**: ASP.NET Core, Entity Framework Core
- **Database**: SQLite
- **Bot Library**: [Telegram.Bot](https://github.com/TelegramBots/Telegram.Bot) (v22)
- **Target**: .NET 10

## License

MIT
