using Microsoft.EntityFrameworkCore;
using Telegram2OpenCode.Components;
using Telegram2OpenCode.Data;
using Telegram2OpenCode.Repositories;
using Telegram2OpenCode.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IAiAgentRepository, AiAgentRepository>();
builder.Services.AddScoped<ITelegramBotRepository, TelegramBotRepository>();

builder.Services.AddHttpClient();
builder.Services.AddSingleton<OpenCodeManager>();
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
