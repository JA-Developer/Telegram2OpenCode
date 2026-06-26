using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
using Microsoft.Extensions.Logging;

namespace Telegram2OpenCode.Services;

public sealed class OpenCodeRunner
{
    private static readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly ILogger<OpenCodeRunner> _logger;

    private static readonly Dictionary<string, Dictionary<string, string>> AgentPermissions = new()
    {
        ["plan"] = new()
        {
            ["glob"] = "allow",
            ["read"] = "allow",
            ["grep"] = "allow",
            ["external_directory"] = "allow"
        },
        ["build"] = new()
        {
            ["glob"] = "allow",
            ["read"] = "allow",
            ["grep"] = "allow",
            ["write"] = "allow",
            ["edit"] = "allow",
            ["bash"] = "allow",
            ["external_directory"] = "allow"
        }
    };

    public OpenCodeRunner(ILogger<OpenCodeRunner> logger)
    {
        _logger = logger;
    }

    public async Task<List<string>> PlanAsync(string argument, CancellationToken cancellationToken = default)
    {
        var envVars = new Dictionary<string, string?>
        {
            ["OPENCODE_PERMISSION"] = JsonSerializer.Serialize(AgentPermissions["plan"])
        };

        await _semaphore.WaitAsync(cancellationToken);

        try
        {
            var results = new List<string>();
            var lineBuffer = new StringBuilder();
            var planArgs = new[] { "run", "--agent", "plan", "--format", "json", argument ?? string.Empty };

            _logger.LogInformation("[opencode/plan] opencode {Args}", string.Join(" ", planArgs));

            await Cli.Wrap("opencode")
                .WithArguments(planArgs)
                .WithEnvironmentVariables(envVars)
                .WithStandardInputPipe(PipeSource.Null)
                .WithStandardOutputPipe(PipeTarget.ToDelegate(chunk =>
                {
                    lineBuffer.Append(chunk);
                    _logger.LogInformation("[opencode/plan] {Chunk}", chunk);
                    var content = lineBuffer.ToString();
                    var lines = content.Split('\n');
                    lineBuffer.Clear();
                    lineBuffer.Append(lines[^1]);
                    for (var i = 0; i < lines.Length - 1; i++)
                        ProcessNdjsonLine(lines[i].TrimEnd('\r'), results);
                }))
                .ExecuteAsync(cancellationToken);

            if (lineBuffer.Length > 0)
            {
                var last = lineBuffer.ToString().TrimEnd('\r');
                if (!string.IsNullOrWhiteSpace(last))
                {
                    _logger.LogInformation("[opencode/plan] {Chunk}", last);
                    ProcessNdjsonLine(last, results);
                }
            }

            return results;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<SessionItem>> ListSessionsAsync(CancellationToken cancellationToken = default)
    {
        var stdOut = new StringBuilder();

        await Cli.Wrap("opencode")
            .WithArguments(new[] { "session", "list", "--format", "json" })
            .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOut))
            .ExecuteAsync(cancellationToken);

        var output = stdOut.ToString();
        if (string.IsNullOrWhiteSpace(output))
            return new List<SessionItem>();

        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        return JsonSerializer.Deserialize<List<SessionItem>>(output, options) ?? new List<SessionItem>();
    }

    public async Task<List<string>> BuildAsync(string argument, string? sessionId = null, CancellationToken cancellationToken = default)
    {
        var envVars = new Dictionary<string, string?>
        {
            ["OPENCODE_PERMISSION"] = JsonSerializer.Serialize(AgentPermissions["build"])
        };

        await _semaphore.WaitAsync(cancellationToken);

        try
        {
            var results = new List<string>();
            var lineBuffer = new StringBuilder();
            var buildArgs = new List<string> { "run", "--agent", "build" };
            if (!string.IsNullOrEmpty(sessionId))
            {
                buildArgs.Add("--session");
                buildArgs.Add(sessionId);
            }
            buildArgs.Add("--format");
            buildArgs.Add("json");
            buildArgs.Add(argument ?? string.Empty);

            _logger.LogInformation("[opencode/build] opencode {Args}", string.Join(" ", buildArgs));

            await Cli.Wrap("opencode")
                .WithArguments(buildArgs)
                .WithEnvironmentVariables(envVars)
                .WithStandardInputPipe(PipeSource.Null)
                .WithStandardOutputPipe(PipeTarget.ToDelegate(chunk =>
                {
                    lineBuffer.Append(chunk);
                    _logger.LogInformation("[opencode/build] {Chunk}", chunk);
                    var content = lineBuffer.ToString();
                    var lines = content.Split('\n');
                    lineBuffer.Clear();
                    lineBuffer.Append(lines[^1]);
                    for (var i = 0; i < lines.Length - 1; i++)
                        ProcessNdjsonLine(lines[i].TrimEnd('\r'), results);
                }))
                .ExecuteAsync(cancellationToken);

            if (lineBuffer.Length > 0)
            {
                var last = lineBuffer.ToString().TrimEnd('\r');
                if (!string.IsNullOrWhiteSpace(last))
                {
                    _logger.LogInformation("[opencode/build] {Chunk}", last);
                    ProcessNdjsonLine(last, results);
                }
            }

            return results;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private static void ProcessNdjsonLine(string line, List<string> results)
    {
        if (string.IsNullOrWhiteSpace(line))
            return;

        try
        {
            using var doc = JsonDocument.Parse(line);
            var json = doc.RootElement;

            if (json.TryGetProperty("type", out var type) && type.GetString() == "text" &&
                json.TryGetProperty("part", out var part) &&
                part.TryGetProperty("text", out var textProp))
            {
                var text = textProp.GetString();
                if (!string.IsNullOrEmpty(text))
                    results.Add(text);
            }
        }
        catch (JsonException)
        {
        }
    }
}
