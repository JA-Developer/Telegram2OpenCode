using PdfSharp.Fonts;
using Telegram2OpenCode.Properties;

public sealed class RobotoMonoFontResolver : IFontResolver
{
    public string DefaultFontName => "OpenCodeFont";

    // PASO 1: PdfSharp pregunta qué fuente usar basado en el estilo solicitado
    public FontResolverInfo ResolveTypeface(string familyName, bool isBold, bool isItalic)
    {
        // Ignoramos el familyName porque forzamos nuestra propia fuente.
        // Evaluamos si el código está pidiendo texto en negrita.
        if (isBold)
        {
            return new FontResolverInfo("OpenCodeFont-Bold");
        }

        // Si no es negrita, devolvemos el identificador de la normal
        return new FontResolverInfo("OpenCodeFont-Regular");
    }

    // PASO 2: PdfSharp pide los bytes exactos usando el identificador del Paso 1
    public byte[]? GetFont(string faceName)
    {
        if (faceName == "OpenCodeFont-Bold")
        {
            return Resource.RobotoMono_Bold;
        }

        if (faceName == "OpenCodeFont-Regular")
        {
            return Resource.RobotoMono_Regular;
        }

        return null;
    }
}