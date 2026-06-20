using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;

namespace Telegram2OpenCode.Services;

public sealed class OpenCodeRunner
{
    private static readonly SemaphoreSlim _semaphore = new(1, 1);

    public async Task<List<string>> RunAsync(string argument, CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken);

        try
        {
            var stdOut = new StringBuilder();

            var permissions = JsonSerializer.Serialize(new
            {
                glob = "allow",
                read = "allow",
                grep = "allow",
                external_directory = "allow"
            });

            await Cli.Wrap("opencode")
                .WithArguments(new[] { "run", "--agent", "plan", "--pure", "--format", "json", argument ?? string.Empty })
                .WithEnvironmentVariables(new Dictionary<string, string?>
                {
                    ["OPENCODE_PERMISSION"] = permissions
                })
                .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOut))
                .ExecuteAsync(cancellationToken);

            var output = stdOut.ToString();
            if (string.IsNullOrWhiteSpace(output))
                return new List<string>();

            var lines = output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var entries = new List<(long timestamp, string text)>();

            foreach (var line in lines)
            {
                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var json = doc.RootElement;

                    if (json.TryGetProperty("type", out var type) && type.GetString() == "text")
                    {
                        if (json.TryGetProperty("part", out var part) && part.TryGetProperty("text", out var textProp))
                        {
                            var extractedText = textProp.GetString();
                            if (!string.IsNullOrEmpty(extractedText))
                            {
                                var timestamp = json.TryGetProperty("timestamp", out var ts) ? ts.GetInt64() : 0;
                                entries.Add((timestamp, extractedText));
                            }
                        }
                    }
                }
                catch (JsonException)
                {
                    continue;
                }
            }

            entries.Sort((a, b) => a.timestamp.CompareTo(b.timestamp));
            return entries.ConvertAll(e => e.text);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
