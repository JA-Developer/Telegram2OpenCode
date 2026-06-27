using System.Reflection;
using PdfSharp.Fonts;

public sealed class RobotoMonoFontResolver : IFontResolver
{
    private static readonly Assembly Assembly = Assembly.GetExecutingAssembly();
    private static readonly Dictionary<string, byte[]> Cache = new();

    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        return new FontResolverInfo(isBold ? "OpenCodeFont-Bold" : "OpenCodeFont-Regular");
    }

    public byte[]? GetFont(string faceName)
    {
        if (Cache.TryGetValue(faceName, out var cached))
            return cached;

        var resourceName = faceName switch
        {
            "OpenCodeFont-Bold" => "Telegram2OpenCode.Resources.RobotoMono-Bold.ttf",
            "OpenCodeFont-Regular" => "Telegram2OpenCode.Resources.RobotoMono-Regular.ttf",
            _ => null
        };

        if (resourceName is null)
            return null;

        using var stream = Assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            return null;

        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var bytes = ms.ToArray();
        Cache[faceName] = bytes;
        return bytes;
    }
}
