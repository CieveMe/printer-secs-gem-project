using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using PrinterSecsGem.Eq.StatusUi;
using Secs4Net;

namespace PrinterSecsGem.Eq.Secs;

public sealed class SecsPrimaryMessageWorker : BackgroundService
{
    private readonly ISecsConnection _secsConnection;
    private readonly ISecsGem _secsGem;
    private readonly SecsMessageDispatcher _dispatcher;
    private readonly StatusUiEventBus _statusEvents;
    private readonly ILogger<SecsPrimaryMessageWorker> _logger;

    public SecsPrimaryMessageWorker(
        ISecsConnection secsConnection,
        ISecsGem secsGem,
        SecsMessageDispatcher dispatcher,
        StatusUiEventBus statusEvents,
        ILogger<SecsPrimaryMessageWorker> logger)
    {
        _secsConnection = secsConnection;
        _secsGem = secsGem;
        _dispatcher = dispatcher;
        _statusEvents = statusEvents;
        _logger = logger;

        _secsConnection.ConnectionChanged += delegate
        {
            _logger.LogInformation("HSMS connection state: {State}", _secsConnection.State);
            _statusEvents.Publish(StatusUiEventCategories.SecsState, _secsConnection.State.ToString());
        };
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting SECS/GEM EQ worker");
        _statusEvents.Publish(StatusUiEventCategories.SecsLog, "Starting SECS/GEM EQ worker.");

        _secsConnection.Start(stoppingToken);

        await foreach (var received in _secsGem.GetPrimaryMessageAsync(stoppingToken))
        {
            using var primaryMessage = received.PrimaryMessage;
            SecsMessage? secondaryMessage = null;

            try
            {
                secondaryMessage = await _dispatcher.DispatchAsync(primaryMessage, stoppingToken);

                if (secondaryMessage is null)
                {
                    _logger.LogWarning("No handler for S{Stream}F{Function}", primaryMessage.S, primaryMessage.F);
                    continue;
                }

                await received.TryReplyAsync(secondaryMessage, stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to process S{Stream}F{Function}", primaryMessage.S, primaryMessage.F);
            }
            finally
            {
                secondaryMessage?.Dispose();
            }
        }
    }
}
