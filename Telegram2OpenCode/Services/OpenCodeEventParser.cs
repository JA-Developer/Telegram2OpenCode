using System.Text.Json;

namespace Telegram2OpenCode.Services;

public sealed record EditEvent(string Tool, string FilePath, string? Patch, int Additions, int Deletions);

public static class OpenCodeEventParser
{
    public static bool TryParseEditEvent(string line, out EditEvent? editEvent)
    {
        editEvent = null;

        if (string.IsNullOrWhiteSpace(line))
            return false;

        try
        {
            using var doc = JsonDocument.Parse(line);
            var json = doc.RootElement;

            // 1. Verificamos que la propiedad exista Y que sea explícitamente un String.
            // Esto evita el InvalidOperationException al llamar a GetString().
            if (!json.TryGetProperty("type", out var typeProp) || typeProp.ValueKind != JsonValueKind.String)
                return false;

            var type = typeProp.GetString();
            if (type != "tool_use")
                return false;

            // 2. Delegamos al siguiente método de parseo.
            return TryParseToolUse(json, out editEvent);
        }
        catch (JsonException)
        {
            // Se captura si el formato de 'line' no es un JSON válido.
            return false;
        }
        catch (Exception)
        {
            // 3. Captura general como red de seguridad definitiva. 
            // Esto garantiza que si TryParseToolUse lanza algo inesperado 
            // (o si hay problemas de memoria), tu sistema no colapsará.
            editEvent = null;
            return false;
        }
    }

    private static bool TryParseToolUse(JsonElement json, out EditEvent? editEvent)
    {
        editEvent = null;

        try
        {
            // 1. Garantizar que 'json' es un objeto antes de buscar en él
            if (json.ValueKind != JsonValueKind.Object)
                return false;

            if (!json.TryGetProperty("part", out var part))
                return false;

            // 2. Garantizar que 'part' es un objeto antes de buscar en él
            if (part.ValueKind != JsonValueKind.Object)
                return false;

            if (!part.TryGetProperty("tool", out var toolProp))
                return false;

            // 3. Garantizar que 'tool' es un string antes de intentar extraerlo
            if (toolProp.ValueKind != JsonValueKind.String)
                return false;

            var tool = toolProp.GetString();

            // 4. Delegamos en las funciones específicas
            return tool switch
            {
                "edit" => TryParseEditTool(part, out editEvent),
                "write" => TryParseWriteTool(part, out editEvent),
                _ => false
            };
        }
        catch (Exception)
        {
            // 5. El Escudo Definitivo. 
            // Si TryParseEditTool o TryParseWriteTool lanzan una excepción no controlada
            // (como NullReferenceException o ArgumentException), la atrapamos aquí
            // para que tu aplicación nunca colapse.
            editEvent = null;
            return false;
        }
    }

    private static bool TryParseEditTool(JsonElement part, out EditEvent? editEvent)
    {
        editEvent = null;

        try
        {
            // 1. Verificación en cascada de que todas las propiedades anidadas son objetos válidos
            if (part.ValueKind != JsonValueKind.Object)
                return false;

            if (!part.TryGetProperty("state", out var state) || state.ValueKind != JsonValueKind.Object)
                return false;

            if (!state.TryGetProperty("metadata", out var metadata) || metadata.ValueKind != JsonValueKind.Object)
                return false;

            if (!metadata.TryGetProperty("filediff", out var filediff) || filediff.ValueKind != JsonValueKind.Object)
                return false;

            // 2. Extracción segura del archivo (Obligatorio)
            string? filePath = null;
            if (filediff.TryGetProperty("file", out var fileProp) && fileProp.ValueKind == JsonValueKind.String)
            {
                filePath = fileProp.GetString();
            }

            if (filePath is null)
                return false;

            // 3. Extracción segura del patch (Opcional)
            string? patch = null;
            if (filediff.TryGetProperty("patch", out var patchProp) && patchProp.ValueKind == JsonValueKind.String)
            {
                patch = patchProp.GetString();
            }

            // 4. Extracción segura de números usando TryGetInt32 para evitar desbordamientos
            int additions = 0;
            if (filediff.TryGetProperty("additions", out var addProp) && addProp.ValueKind == JsonValueKind.Number)
            {
                addProp.TryGetInt32(out additions);
            }

            int deletions = 0;
            if (filediff.TryGetProperty("deletions", out var delProp) && delProp.ValueKind == JsonValueKind.Number)
            {
                delProp.TryGetInt32(out deletions);
            }

            // 5. Instanciación final
            editEvent = new EditEvent("edit", filePath, patch, additions, deletions);
            return true;
        }
        catch (Exception)
        {
            // 6. El Escudo Definitivo. 
            // Atrapa cualquier excepción imprevista (por ejemplo, si el constructor de EditEvent falla).
            editEvent = null;
            return false;
        }
    }

    private static bool TryParseWriteTool(JsonElement part, out EditEvent? editEvent)
    {
        editEvent = null;

        try
        {
            // 1. Validar la cadena principal de objetos
            if (part.ValueKind != JsonValueKind.Object)
                return false;

            if (!part.TryGetProperty("state", out var state) || state.ValueKind != JsonValueKind.Object)
                return false;

            if (!state.TryGetProperty("metadata", out var metadata) || metadata.ValueKind != JsonValueKind.Object)
                return false;

            // 2. Extraer el filepath principal de forma segura
            string? filePath = null;
            if (metadata.TryGetProperty("filepath", out var fp) && fp.ValueKind == JsonValueKind.String)
            {
                filePath = fp.GetString();
            }

            int additions = 0;

            // 3. Procesar el bloque 'input' si existe y es un objeto válido
            if (state.TryGetProperty("input", out var input) && input.ValueKind == JsonValueKind.Object)
            {
                // Fallback de filePath
                if (filePath is null)
                {
                    if (input.TryGetProperty("filePath", out var fpp) && fpp.ValueKind == JsonValueKind.String)
                    {
                        filePath = fpp.GetString();
                    }
                }

                // 4. Extracción y cálculo seguro del contenido
                if (input.TryGetProperty("content", out var content) && content.ValueKind == JsonValueKind.String)
                {
                    var text = content.GetString();
                    if (text is not null)
                    {
                        additions = text.Count(c => c == '\n') + 1;
                    }
                }
            }

            // Si después de buscar en ambos lados no hay ruta, abortamos
            if (filePath is null)
                return false;

            // 5. Instanciación final
            editEvent = new EditEvent("write", filePath, null, additions, 0);
            return true;
        }
        catch (Exception)
        {
            // 6. El Escudo Definitivo. 
            // Atrapa fallos de memoria, errores en el constructor de EditEvent,
            // o cualquier anomalía insospechada en el conteo de líneas.
            editEvent = null;
            return false;
        }
    }
}
