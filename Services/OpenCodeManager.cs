using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Telegram2OpenCode.Services;

public sealed class OpenCodeManager
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _apiUrl;

    public OpenCodeManager(IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _apiUrl = configuration["OpenCode:ApiUrl"] ?? "http://localhost:5000";
    }

    public async Task<string?> CreateSessionAsync(string title, CancellationToken cancellationToken = default)
    {
        var httpClient = _httpClientFactory.CreateClient();
        var response = await httpClient.PostAsJsonAsync(
            $"{_apiUrl}/session",
            new { title },
            cancellationToken
        );

        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<CreateSessionResponse>(cancellationToken: cancellationToken);
        return data?.Id;
    }

    public async Task<string?> SendMessageAsync(string sessionId, string message, CancellationToken cancellationToken = default)
    {
        var httpClient = _httpClientFactory.CreateClient();
        var payload = new
        {
            parts = new[]
            {
                new { type = "text", text = message }
            }
        };

        var response = await httpClient.PostAsJsonAsync(
            $"{_apiUrl}/session/{sessionId}/message",
            payload,
            cancellationToken
        );

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<OpenCodeMessageResponse>(cancellationToken: cancellationToken);

        return result?.Parts
            ?.Where(p => p.Type == "text")
            ?.Select(p => p.Text)
            ?.FirstOrDefault();
    }

    private sealed class CreateSessionResponse
    {
        public string? Id { get; set; }
    }

    private sealed class OpenCodeMessageResponse
    {
        public OpenCodeMessageInfo? Info { get; set; }
        public List<OpenCodeMessagePart>? Parts { get; set; }
    }

    private sealed class OpenCodeMessageInfo
    {
        public string? Id { get; set; }
        public string? SessionID { get; set; }
        public string? Role { get; set; }
        public string? Status { get; set; }
    }

    private sealed class OpenCodeMessagePart
    {
        public string Type { get; set; } = string.Empty;
        public string? Text { get; set; }
    }
}
