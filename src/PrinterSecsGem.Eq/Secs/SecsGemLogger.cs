using Microsoft.Extensions.Logging;
using Secs4Net;

namespace PrinterSecsGem.Eq.Secs;

internal sealed class SecsGemLogger : ISecsGemLogger
{
    private readonly ILogger<SecsGemLogger> _logger;

    public SecsGemLogger(ILogger<SecsGemLogger> logger)
    {
        _logger = logger;
    }

    public void MessageIn(SecsMessage msg, int id)
    {
        _logger.LogTrace("<-- [0x{MessageId:X8}] {Message}", id, msg);
    }

    public void MessageOut(SecsMessage msg, int id)
    {
        _logger.LogTrace("--> [0x{MessageId:X8}] {Message}", id, msg);
    }

    public void Debug(string msg)
    {
        _logger.LogDebug("{Message}", msg);
    }

    public void Info(string msg)
    {
        _logger.LogInformation("{Message}", msg);
    }

    public void Warning(string msg)
    {
        _logger.LogWarning("{Message}", msg);
    }

    public void Error(string msg, SecsMessage? message, Exception? ex)
    {
        _logger.LogError(ex, "{Message} {SecsMessage}", msg, message);
    }
}
