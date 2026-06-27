using Microsoft.EntityFrameworkCore;
using Telegram2OpenCode.Components;
using Telegram2OpenCode.Data;
using Telegram2OpenCode.Repositories;
using Telegram2OpenCode.Services;
using Telegram2OpenCode.Services.Handlers;
using Telegram2OpenCode.Services.OpenCodeServerLoader;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IAiAgentRepository, AiAgentRepository>();
builder.Services.AddScoped<ITelegramBotRepository, TelegramBotRepository>();
builder.Services.AddScoped<IChatSessionRepository, ChatSessionRepository>();

builder.Services.AddHttpClient("OpenCode")
    .ConfigureHttpClient(c => c.Timeout = System.Threading.Timeout.InfiniteTimeSpan);
builder.Services.AddSingleton<OpenCodeManager>();
builder.Services.AddSingleton<OpenCodeRunner>();
builder.Services.AddSingleton<VibeUtils>();
builder.Services.AddSingleton<ChatSessionService>();
builder.Services.AddSingleton<IStateHandler, InitialMenuHandler>();
builder.Services.AddSingleton<IStateHandler, SelectingSessionHandler>();
builder.Services.AddSingleton<IStateHandler, AwaitingFolderHandler>();
builder.Services.AddSingleton<IStateHandler, ChatHandler>();
builder.Services.AddSingleton<StateHandlerResolver>();
builder.Services.Configure<OpenCodeServerOptions>(builder.Configuration.GetSection("OpenCodeServer"));
builder.Services.AddHostedService<OpenCodeServerLoadService>();
builder.Services.AddHostedService<BotService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseHttpsRedirection();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
