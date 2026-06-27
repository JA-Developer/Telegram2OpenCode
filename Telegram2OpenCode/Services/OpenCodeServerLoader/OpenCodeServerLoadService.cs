using CliWrap;
using Microsoft.Extensions.Options;
using Polly;

namespace Telegram2OpenCode.Services.OpenCodeServerLoader
{
    public class OpenCodeServerLoadService : IHostedService
    {
        private readonly OpenCodeServerOptions _options;
        private readonly ILogger<OpenCodeServerLoadService> _logger;
        private readonly IHttpClientFactory _httpClientFactory;

        // Guards concurrent access to _processCts and _processTask
        private readonly object _processLock = new();

        private CancellationTokenSource? _processCts = null; // Controls the child process lifetime
        private Task? _processTask = null;                   // Task wrapping process execution

        public OpenCodeServerLoadService(
            IOptions<OpenCodeServerOptions> options,
            ILogger<OpenCodeServerLoadService> logger,
            IHttpClientFactory httpClientFactory)
        {
            _options = options.Value;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            var address = string.IsNullOrWhiteSpace(_options.ServerAddress)
                ? "http://127.0.0.1"
                : (_options.ServerAddress?.TrimEnd('/') ?? "http://127.0.0.1");
            var port = (_options.ServerPort ?? 0) > 0
                ? (_options.ServerPort ?? 4096)
                : 4096;
            var serviceUrl = $"{address}:{port}/global/health";

            if (await IsServiceAlreadyRunningAsync(serviceUrl, cancellationToken))
            {
                _logger.LogInformation("Service is already running at {ServiceUrl}.", serviceUrl);
                return;
            }

            var retryPolicy = Policy
                .Handle<Exception>(ex =>
                    ex is not OperationCanceledException &&
                    ex is not TaskCanceledException)
                .WaitAndRetryAsync(
                    retryCount: 3,
                    sleepDurationProvider: _ => TimeSpan.FromSeconds(5),
                    onRetry: (exception, timeSpan, retryCount, _) =>
                    {
                        _logger.LogWarning(
                            "Attempt {Count} failed. Retrying in {Wait}s... Error: {Msg}",
                            retryCount, timeSpan.TotalSeconds, exception.Message);
                    });

            await retryPolicy.ExecuteAsync(async () =>
            {
                // Cancel and wait for any previous process before starting a new one
                await CancelExistingProcessAsync();

                var stdErrBuffer = new StringWriter();
                var stdErrLock = new object(); // Local lock for concurrent writes to the buffer

                // Create a CTS linked to the external token so the process can be cancelled individually
                lock (_processLock)
                {
                    _processCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                }

                var processToken = _processCts.Token;

                // Run OpenCode Server in the background; the process runs until processToken is cancelled
                var hostname = Uri.TryCreate(address, UriKind.Absolute, out var uri) ? uri.Host : address;
                var serveArgs = new List<string> { "serve", "--port", port.ToString(), "--hostname", hostname };

                _processTask = Task.Run(async () =>
                {
                    try
                    {
                        var result = await Cli
                            .Wrap("opencode")
                            .WithArguments(serveArgs)
                            // Capture stderr without blocking to diagnose process failures
                            .WithStandardErrorPipe(PipeTarget.ToDelegate(line =>
                            {
                                lock (stdErrLock)
                                {
                                    stdErrBuffer.WriteLine(line);
                                }
                            }))
                            .WithValidation(CommandResultValidation.None) // Handle exit code manually
                            .ExecuteAsync(processToken);

                        // Only log an error if the process exited unexpectedly (not due to cancellation)
                        if (result.ExitCode != 0 && !processToken.IsCancellationRequested)
                        {
                            string captured;
                            lock (stdErrLock)
                            {
                                captured = stdErrBuffer.ToString();
                            }

                            _logger.LogError(
                                "OpenCode Server exited unexpectedly (code {Code}): {Err}",
                                result.ExitCode, captured);
                        }
                    }
                    catch (OperationCanceledException) { /* Normal cancellation; do not log */ }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Unhandled exception in OpenCode Server process.");
                    }
                }, CancellationToken.None); // CancellationToken.None: the task is not cancelled before it starts

                // Allow the process time to initialize before the health check
                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // External cancellation during wait: clean up the process and propagate
                    await CancelExistingProcessAsync();
                    throw;
                }

                // Verify the process responded correctly after startup
                if (!await IsServiceAlreadyRunningAsync(serviceUrl, cancellationToken))
                    throw new HttpRequestException(
                        $"OpenCode Server did not respond after startup at {serviceUrl}.");

                _logger.LogInformation("OpenCode Server is running at {Url}.", serviceUrl);
            });
        }

        // ─── Helpers ────────────────────────────────────────────────────────────────

        /// <summary>
        /// Cancels the running process and waits for it to exit to avoid orphaned processes.
        /// Thread-safe: the lock ensures only one thread manipulates _processCts / _processTask.
        /// </summary>
        private async Task CancelExistingProcessAsync()
        {
            Task? taskToAwait = null;

            lock (_processLock)
            {
                if (_processCts != null)
                {
                    _processCts.Cancel();
                    _processCts.Dispose();
                    _processCts = null;
                }
                taskToAwait = _processTask;
                _processTask = null;
            }

            if (taskToAwait != null)
            {
                try { await taskToAwait; }
                catch { /* Exceptions already logged inside _processTask */ }
            }
        }

        /// <summary>
        /// Checks whether OpenCode Server is already serving requests at <paramref name="serviceUrl"/>.
        /// Distinguishes HttpClient timeout (treat as retry) from external cancellation (propagated).
        /// </summary>
        private async Task<bool> IsServiceAlreadyRunningAsync(
            string serviceUrl, CancellationToken cancellationToken)
        {
            try
            {
                using var client = _httpClientFactory.CreateClient();
                client.Timeout = TimeSpan.FromSeconds(5);
                var response = await client.GetAsync(serviceUrl, cancellationToken);

                if (response.IsSuccessStatusCode)
                    return true;

                _logger.LogWarning(
                    "Service returned non-success status {StatusCode}. OpenCode Server will be started.",
                    response.StatusCode);
                return false;
            }
            catch (HttpRequestException ex)
            {
                // Connection refused or network error: OpenCode Server is not running
                _logger.LogInformation(
                    "Service is not responding ({Msg}). Starting OpenCode Server...", ex.Message);
                return false;
            }
            catch (TaskCanceledException ex)
            {
                // Distinguish HttpClient timeout (retry) from external cancellation (propagate)
                if (cancellationToken.IsCancellationRequested)
                    throw new OperationCanceledException(
                        "Service health check was cancelled externally.", ex, cancellationToken);

                _logger.LogInformation("Timeout while checking service. Starting OpenCode Server...");
                return false;
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            await CancelExistingProcessAsync();
        }
    }
}
