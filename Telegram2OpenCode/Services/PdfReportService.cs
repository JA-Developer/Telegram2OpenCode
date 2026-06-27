using PdfSharp.Drawing;
using PdfSharp.Fonts;
using PdfSharp.Pdf;

namespace Telegram2OpenCode.Services;

public sealed class PdfReportService
{
    private static readonly string _fontName;

    static PdfReportService()
    {
        GlobalFontSettings.FontResolver = new RobotoMonoFontResolver();
        _fontName = "OpenCodeFont";
    }

    public byte[] GenerateReport(List<EditEvent>? editEvents, string? sessionId)
    {
        // Guard: empty input returns empty byte array
        if (editEvents is null || editEvents.Count == 0)
            return Array.Empty<byte>();

        sessionId ??= "Unknown-Session";

        using var doc = new PdfDocument();
        doc.Info.Title = "OpenCode - Cambios Realizados";

        // Font setup
        var titleFont = new XFont(_fontName, 14, XFontStyleEx.Bold);
        var headerFont = new XFont(_fontName, 10, XFontStyleEx.Bold);
        var bodyFont = new XFont(_fontName, 8, XFontStyleEx.Regular);
        var metaFont = new XFont(_fontName, 9, XFontStyleEx.Regular);

        const double margin = 40;
        double y = margin;
        double pageHeight = 0;

        PdfPage? page = null;
        XGraphics? gfx = null;

        try
        {
            void AddNewPage()
            {
                gfx?.Dispose();
                page = doc.AddPage();
                pageHeight = page.Height.Point;
                gfx = XGraphics.FromPdfPage(page);
                y = margin;
            }

            AddNewPage();

            // Title
            gfx!.DrawString("OpenCode \u2013 Cambios Realizados", titleFont, XBrushes.Black, margin, y);
            y += 30;

            // Session metadata
            gfx.DrawString($"Session: {sessionId}", metaFont, XBrushes.DimGray, margin, y);
            y += 16;
            gfx.DrawString($"Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}", metaFont, XBrushes.DimGray, margin, y);
            y += 16;

            int validFilesCount = editEvents.Count(e => e != null);
            gfx.DrawString($"Files modified: {validFilesCount}", metaFont, XBrushes.DimGray, margin, y);
            y += 24;

            // Separator
            gfx.DrawLine(new XPen(XColors.LightGray, 0.5), margin, y, page!.Width.Point - margin, y);
            y += 16;

            foreach (var edit in editEvents)
            {
                if (edit is null) continue;

                // Page break before entry if nearing bottom
                if (y > pageHeight - 80)
                {
                    AddNewPage();
                }

                var toolName = edit.Tool ?? string.Empty;
                var icon = toolName.ToLowerInvariant() switch
                {
                    "write" => "[NEW]",
                    _ when edit.Deletions > 0 && edit.Additions == 0 => "[DEL]",
                    _ when edit.Additions > 0 && edit.Deletions == 0 => "[ADD]",
                    _ => "[MOD]"
                };

                var filePath = edit.FilePath ?? "Unknown_Path";
                gfx!.DrawString($"{icon}  {filePath}", headerFont, XBrushes.Black, margin, y);
                y += 14;

                gfx.DrawString($"+{edit.Additions}  -{edit.Deletions}  lines", bodyFont, XBrushes.DimGray, margin + 10, y);
                y += 20;

                // Render patch with line-level color coding
                if (!string.IsNullOrWhiteSpace(edit.Patch))
                {
                    var lines = edit.Patch.Split('\n', StringSplitOptions.None);
                    foreach (var line in lines)
                    {
                        if (y > pageHeight - 40)
                        {
                            AddNewPage();
                        }

                        var text = line.TrimEnd('\r');

                        // Truncate lines > 120 chars
                        if (text.Length > 120)
                            text = string.Concat(text.AsSpan(0, 117), "...");

                        // Color by diff prefix: green (+), red (-), blue (@@), black (context)
                        XBrush color = text.Length > 0 ? text[0] switch
                        {
                            '+' => XBrushes.DarkGreen,
                            '-' => XBrushes.DarkRed,
                            '@' when text.StartsWith("@@", StringComparison.Ordinal) => XBrushes.DarkBlue,
                            _ => XBrushes.Black
                        } : XBrushes.Black;

                        gfx!.DrawString(text, bodyFont, color, margin + 8, y);
                        y += 12;
                    }
                    y += 8;
                }
            }
        }
        finally
        {
            gfx?.Dispose();
        }

        using var ms = new MemoryStream();
        doc.Save(ms);
        return ms.ToArray();
    }
}
