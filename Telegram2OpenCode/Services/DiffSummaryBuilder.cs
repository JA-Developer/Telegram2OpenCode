using System.Text;

namespace Telegram2OpenCode.Services;

public sealed class DiffSummaryBuilder
{
    public string? Build(List<EditEvent> editEvents)
    {
        if (editEvents.Count == 0)
            return null;

        var lines = new StringBuilder();
        lines.AppendLine("━━━ Modified Files ━━━");
        foreach (var edit in editEvents)
        {
            var icon = edit.Tool == "write" ? "🆕" : edit.Deletions > 0 && edit.Additions == 0 ? "➖" : edit.Additions > 0 && edit.Deletions == 0 ? "➕" : "✏️";
            lines.AppendLine($"{icon} {edit.FilePath} (+{edit.Additions}/-{edit.Deletions})");
        }
        return lines.ToString();
    }
}
