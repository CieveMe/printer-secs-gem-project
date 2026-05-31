using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Secs4Net;

namespace PrinterSecsGem.Eq.Secs;

public sealed class SecsPrimaryMessageWorker : BackgroundService
{
    private readonly ISecsConnection _secsConnection;
    private readonly ISecsGem _secsGem;
    private readonly SecsMessageDispatcher _dispatcher;
    private readonly ILogger<SecsPrimaryMessageWorker> _logger;

    public SecsPrimaryMessageWorker(
        ISecsConnection secsConnection,
        ISecsGem secsGem,
        SecsMessageDispatcher dispatcher,
        ILogger<SecsPrimaryMessageWorker> logger)
    {
        _secsConnection = secsConnection;
        _secsGem = secsGem;
        _dispatcher = dispatcher;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Starting SECS/GEM EQ worker");

        _secsConnection.LinkTestEnabled = true;
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

                await received.TryReplyAsync(secondaryMessage);
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
