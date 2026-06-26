using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;

namespace Telegram2OpenCode.Services;

public sealed class OpenCodeManager
{
    private readonly OpenCodeRunner _runner;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly string _apiUrl;

    public OpenCodeManager(OpenCodeRunner runner, IHttpClientFactory httpClientFactory, IConfiguration configuration)
    {
        _runner = runner;
        _httpClientFactory = httpClientFactory;
        _apiUrl = configuration["OpenCode:ApiUrl"] ?? "http://localhost:5000";
    }

    public async Task<string?> CreateSessionAsync(CreateSessionRequest request, CancellationToken cancellationToken = default)
    {
        var httpClient = _httpClientFactory.CreateClient("OpenCode");
        var url = $"{_apiUrl}/session{request.ToQueryString()}";
        var response = await httpClient.PostAsJsonAsync(
            url,
            request.ToBody(),
            cancellationToken
        );

        response.EnsureSuccessStatusCode();
        var data = await response.Content.ReadFromJsonAsync<CreateSessionResponse>(cancellationToken: cancellationToken);
        return data?.Id;
    }

    public async Task<string?> SendMessageAsync(string sessionId, string message, CancellationToken cancellationToken = default)
    {
        var outputs = await _runner.BuildAsync(message, sessionId, cancellationToken);
        return outputs.LastOrDefault();
    }

    public async Task<List<SessionItem>> GetSessionsAsync(CancellationToken cancellationToken = default)
    {
        return await _runner.ListSessionsAsync(cancellationToken);
    }
    
    private sealed class CreateSessionResponse
    {
        public string? Id { get; set; }
    }
}

public sealed class SessionItem
{
    public string Id { get; set; } = string.Empty;
    public string? Title { get; set; }
}

public sealed class CreateSessionRequest
{
    public string title { get; set; } = string.Empty;
    public string? directory { get; set; }
    public string? workspace { get; set; }
    public string? agent { get; set; }
    public string? parentID { get; set; }
    public string? workspaceID { get; set; }
    public object? model { get; set; }
    public object? metadata { get; set; }
    public object? permission { get; set; }

    internal string ToQueryString()
    {
        var query = System.Web.HttpUtility.ParseQueryString("");
        if (!string.IsNullOrEmpty(directory)) query["directory"] = directory;
        if (!string.IsNullOrEmpty(workspace)) query["workspace"] = workspace;
        return query.Count > 0 ? "?" + query.ToString() : "";
    }

    internal object ToBody()
    {
        var body = new Dictionary<string, object?>
        {
            ["title"] = title
        };

        if (!string.IsNullOrEmpty(agent)) body["agent"] = agent;
        if (!string.IsNullOrEmpty(parentID)) body["parentID"] = parentID;
        if (!string.IsNullOrEmpty(workspaceID)) body["workspaceID"] = workspaceID;
        if (model is not null) body["model"] = model;
        if (metadata is not null) body["metadata"] = metadata;
        if (permission is not null) body["permission"] = permission;

        return body;
    }
}
