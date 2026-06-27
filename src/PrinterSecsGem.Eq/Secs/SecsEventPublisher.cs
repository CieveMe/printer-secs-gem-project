using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrinterSecsGem.Eq.StatusUi;
using Secs4Net;

namespace PrinterSecsGem.Eq.Secs;

public sealed class SecsEventPublisher : BackgroundService
{
    private readonly Channel<SecsMessage> _events = Channel.CreateBounded<SecsMessage>(
        new BoundedChannelOptions(100)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.Wait
        });
    private readonly ISecsGem _secsGem;
    private readonly SecsEventOptions _options;
    private readonly StatusUiEventBus _statusEvents;
    private readonly ILogger<SecsEventPublisher> _logger;

    public SecsEventPublisher(
        ISecsGem secsGem,
        IOptions<SecsEventOptions> options,
        StatusUiEventBus statusEvents,
        ILogger<SecsEventPublisher> logger)
    {
        _secsGem = secsGem;
        _options = options.Value;
        _statusEvents = statusEvents;
        _logger = logger;
    }

    public bool TryPublish(SecsMessage message)
    {
        if (!_options.ActiveReportsEnabled)
        {
            message.Dispose();
            return false;
        }

        if (_events.Writer.TryWrite(message))
        {
            _statusEvents.Publish(
                StatusUiEventCategories.SecsLog,
                $"Queued active report S{message.S}F{message.F} {message.Name}.");
            return true;
        }

        _logger.LogWarning(
            "SECS active report queue is full; dropping S{Stream}F{Function} {Name}",
            message.S,
            message.F,
            message.Name);
        message.Dispose();
        return false;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await foreach (var message in _events.Reader.ReadAllAsync(stoppingToken))
        {
            using (message)
            {
                try
                {
                    _logger.LogInformation(
                        "Sending SECS active report: S{Stream}F{Function} {Name}",
                        message.S,
                        message.F,
                        message.Name);

                    using var reply = await _secsGem.SendAsync(message, stoppingToken);

                    _statusEvents.Publish(
                        StatusUiEventCategories.SecsLog,
                        $"Active report S{message.S}F{message.F} {message.Name} sent.");
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to send SECS active report: S{Stream}F{Function} {Name}",
                        message.S,
                        message.F,
                        message.Name);
                    _statusEvents.Publish(
                        StatusUiEventCategories.SecsLog,
                        $"Active report S{message.S}F{message.F} {message.Name} failed: {ex.Message}");
                }
            }
        }
    }
}
