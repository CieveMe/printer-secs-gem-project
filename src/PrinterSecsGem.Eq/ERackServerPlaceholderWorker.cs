using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace PrinterSecsGem.Eq;

public sealed class ERackServerPlaceholderWorker : BackgroundService
{
    private readonly RuntimeOptions _runtimeOptions;
    private readonly ILogger<ERackServerPlaceholderWorker> _logger;

    public ERackServerPlaceholderWorker(
        IOptions<RuntimeOptions> runtimeOptions,
        ILogger<ERackServerPlaceholderWorker> logger)
    {
        _runtimeOptions = runtimeOptions.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_runtimeOptions.IsServerEnabled)
        {
            return;
        }

        _logger.LogWarning(
            "Runtime mode {RuntimeMode} requested ERACK Server, but TCP server routing is waiting for the reference protocol code. Unit mode services run only when Runtime:Mode is Unit or Both.",
            _runtimeOptions.NormalizedMode);

        try
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
