using System.Net;
using System.Net.Sockets;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrinterSecsGem.Eq.StatusUi;

namespace PrinterSecsGem.Eq.ErackNetwork;

public sealed class ERackServerWorker : BackgroundService
{
    private readonly RuntimeOptions _runtimeOptions;
    private readonly ERackServerOptions _options;
    private readonly ERackTcpUnitRouter _router;
    private readonly StatusUiEventBus _statusEvents;
    private readonly ILogger<ERackServerWorker> _logger;

    public ERackServerWorker(
        IOptions<RuntimeOptions> runtimeOptions,
        IOptions<ERackServerOptions> options,
        ERackTcpUnitRouter router,
        StatusUiEventBus statusEvents,
        ILogger<ERackServerWorker> logger)
    {
        _runtimeOptions = runtimeOptions.Value;
        _options = options.Value;
        _router = router;
        _statusEvents = statusEvents;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_runtimeOptions.IsServerEnabled)
        {
            return;
        }

        var listenAddress = ResolveListenAddress();
        var listener = new TcpListener(listenAddress, _options.Port);
        listener.Start();
        _logger.LogInformation(
            "ERACK Server started: listen={ListenIp}:{Port}",
            listenAddress,
            _options.Port);
        _statusEvents.Publish(
            StatusUiEventCategories.ERackServerStatus,
            $"Listening {listenAddress}:{_options.Port}");

        try
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                var tcpClient = await listener.AcceptTcpClientAsync(stoppingToken);
                _ = Task.Run(
                    () => _router.HandleClientAsync(tcpClient, stoppingToken),
                    stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
        finally
        {
            listener.Stop();
            _logger.LogInformation("ERACK Server stopped.");
            _statusEvents.Publish(StatusUiEventCategories.ERackServerStatus, "Stopped");
        }
    }

    private IPAddress ResolveListenAddress()
    {
        if (string.IsNullOrWhiteSpace(_options.ListenIp) ||
            _options.ListenIp.Equals("*", StringComparison.OrdinalIgnoreCase))
        {
            return IPAddress.Any;
        }

        return IPAddress.Parse(_options.ListenIp);
    }
}
