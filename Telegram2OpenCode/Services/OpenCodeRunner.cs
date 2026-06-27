using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CliWrap;
using CliWrap.EventStream; // REQUERIDO para la lectura asíncrona segura
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

    public async Task<List<string>> PlanAsync(string argument, Func<string, Task>? onUpdateReceived = null, CancellationToken cancellationToken = default)
    {
        var envVars = new Dictionary<string, string?>
        {
            ["OPENCODE_PERMISSION"] = JsonSerializer.Serialize(AgentPermissions["plan"])
        };

        var results = new List<string>();

        // Evitamos enviar strings vacíos como argumentos mal formados
        var planArgs = new List<string> { "run", "--agent", "plan", "--format", "json" };
        if (!string.IsNullOrWhiteSpace(argument))
            planArgs.Add(argument);

        await _semaphore.WaitAsync(cancellationToken);

        try
        {
            _logger.LogInformation("[opencode/plan] opencode {Args}", string.Join(" ", planArgs));

            var cmd = Cli.Wrap("opencode")
                .WithArguments(planArgs)
                .WithEnvironmentVariables(envVars)
                .WithStandardInputPipe(PipeSource.Null)
                .WithValidation(CommandResultValidation.None); // Evita que CliWrap lance excepciones si el exit code no es 0

            // EventStream nos da líneas completas y permite usar 'await' internamente sin bloqueos
            await foreach (var cmdEvent in cmd.ListenAsync(cancellationToken))
            {
                if (cmdEvent is StandardOutputCommandEvent stdOut)
                {
                    var line = stdOut.Text;
                    _logger.LogInformation("[opencode/plan] {Line}", line);

                    var result = ProcessNdjsonLine(line);
                    if (result != null)
                        results.Add(result);

                    // AHORA es seguro ejecutar métodos asíncronos y esperar su resultado
                    if (onUpdateReceived != null)
                    {
                        try
                        {
                            await onUpdateReceived(line);
                        }
                        catch (Exception cbEx)
                        {
                            _logger.LogError(cbEx, "[opencode/plan] Error en el callback onMessageReceived.");
                        }
                    }
                }
                else if (cmdEvent is StandardErrorCommandEvent stdErr)
                {
                    _logger.LogWarning("[opencode/plan] Error output: {Line}", stdErr.Text);
                }
            }

            return results;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("[opencode/plan] Operación cancelada.");
            throw; // Re-lanzar es lo correcto para la cancelación
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[opencode/plan] Fallo crítico al ejecutar opencode.");
            return results; // Retornamos lo que logramos recuperar para no fallar jamás
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<List<SessionItem>> ListSessionsAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var stdOut = new StringBuilder();

            await Cli.Wrap("opencode")
                .WithArguments(new[] { "session", "list", "--format", "json" })
                .WithStandardOutputPipe(PipeTarget.ToStringBuilder(stdOut))
                .WithValidation(CommandResultValidation.None) // Seguridad anti-crashes
                .ExecuteAsync(cancellationToken);

            var output = stdOut.ToString();
            if (string.IsNullOrWhiteSpace(output))
                return new List<SessionItem>();

            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<List<SessionItem>>(output, options) ?? new List<SessionItem>();
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "[opencode/session] Error parseando la salida JSON. Posible texto de error del CLI.");
            return new List<SessionItem>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[opencode/session] Fallo al listar sesiones.");
            return new List<SessionItem>();
        }
    }

    public async Task<List<string>> BuildAsync(string argument, string? sessionId = null, Func<string, Task>? onUpdateReceived = null, CancellationToken cancellationToken = default)
    {
        var envVars = new Dictionary<string, string?>
        {
            ["OPENCODE_PERMISSION"] = JsonSerializer.Serialize(AgentPermissions["build"])
        };

        var results = new List<string>();
        var buildArgs = new List<string> { "run", "--agent", "build" };

        if (!string.IsNullOrEmpty(sessionId))
        {
            buildArgs.Add("--session");
            buildArgs.Add(sessionId);
        }

        buildArgs.Add("--format");
        buildArgs.Add("json");

        if (!string.IsNullOrWhiteSpace(argument))
            buildArgs.Add(argument);

        // 1. Preparamos el Watchdog: 300 segundos de margen entre líneas
        var timeout = TimeSpan.FromSeconds(300);
        using var timeoutCts = new CancellationTokenSource();
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        // Iniciamos la cuenta regresiva inicial
        timeoutCts.CancelAfter(timeout);

        await _semaphore.WaitAsync(linkedCts.Token);

        try
        {
            _logger.LogInformation("[opencode/build] opencode {Args}", string.Join(" ", buildArgs));

            var cmd = Cli.Wrap("opencode")
                .WithArguments(buildArgs)
                .WithEnvironmentVariables(envVars)
                .WithStandardInputPipe(PipeSource.Null)
                .WithValidation(CommandResultValidation.None);

            await foreach (var cmdEvent in cmd.ListenAsync(linkedCts.Token))
            {
                // 2. REINICIO DEL WATCHDOG: Se recibió output, reseteamos el temporizador
                timeoutCts.CancelAfter(timeout);

                if (cmdEvent is StandardOutputCommandEvent stdOut)
                {
                    var line = stdOut.Text;
                    _logger.LogInformation("[opencode/build] {Line}", line);

                    var result = ProcessNdjsonLine(line);
                    if (result != null)
                        results.Add(result);

                    if (onUpdateReceived != null)
                    {
                        try
                        {
                            await onUpdateReceived(line);
                        }
                        catch (Exception cbEx)
                        {
                            _logger.LogError(cbEx, "[opencode/build] Error en el callback onMessageReceived.");
                        }
                    }
                }
                else if (cmdEvent is StandardErrorCommandEvent stdErr)
                {
                    _logger.LogWarning("[opencode/build] Error output: {Line}", stdErr.Text);
                }
            }

            return results;
        }
        catch (OperationCanceledException)
        {
            // Si el token global fue cancelado, el usuario lo pidió. 
            // Si no, fue el timeoutCts quien se disparó por inactividad.
            if (!cancellationToken.IsCancellationRequested)
            {
                _logger.LogError("[opencode/build] Timeout: No se recibió salida en {Timeout}s", timeout);
            }
            else
            {
                _logger.LogWarning("[opencode/build] Operación cancelada.");
            }
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[opencode/build] Fallo crítico al ejecutar opencode build.");
            return results;
        }
        finally
        {
            _semaphore.Release();
        }
    }

    private static string? ProcessNdjsonLine(string line)
    {
        if (string.IsNullOrWhiteSpace(line))
            return null;

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
                    return text;
            }
        }
        catch (JsonException)
        {
            // Omitimos silenciosamente las líneas que no son JSON válido 
            // (a veces los CLIs tiran logs de debug en texto plano)
        }

        return null;
    }
}