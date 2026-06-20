using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Telegram2OpenCode.Services;

public sealed class VibeUtils
{
    private readonly OpenCodeRunner _runner;

    public VibeUtils(OpenCodeRunner runner)
    {
        _runner = runner;
    }

    public async Task<string?> ConvertPromptToPath(string userMessage, CancellationToken cancellationToken = default)
    {
        var prompt = $"@explore Analyze the following user message and determine if it refers to a system folder. If it does, search for it using the available tools (e.g., glob) and respond only with a JSON in this format: {{\"path\": \"absolute_path_to_folder\"}}. If it does not refer to any folder, respond only with: {{}}. User message: {userMessage}";

        var outputs = await _runner.RunAsync(prompt, cancellationToken);

        var jsonText = outputs.LastOrDefault();
        if (jsonText is null)
            return null;

        using var doc = JsonDocument.Parse(jsonText);
        var root = doc.RootElement;

        string? path = null;
        foreach (var prop in root.EnumerateObject())
        {
            if (string.Equals(prop.Name, "path", StringComparison.OrdinalIgnoreCase))
            {
                path = prop.Value.GetString();
                break;
            }
        }

        if (string.IsNullOrEmpty(path))
            return null;

        path = path.Replace("\\\\", "\\");

        if (!Directory.Exists(path))
            throw new DirectoryNotFoundException($"The folder '{path}' does not exist.");

        return path;
    }
}