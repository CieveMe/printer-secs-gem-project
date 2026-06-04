using System.Reflection;
using log4net;
using log4net.Config;
using log4net.Repository;
using Microsoft.Extensions.Logging;
using Log4NetLevel = log4net.Core.Level;
using MsEventId = Microsoft.Extensions.Logging.EventId;
using MsILogger = Microsoft.Extensions.Logging.ILogger;
using MsLogLevel = Microsoft.Extensions.Logging.LogLevel;

namespace PrinterSecsGem.Eq.Logging;

public sealed class Log4NetLoggerProvider : ILoggerProvider
{
    private readonly ILoggerRepository _repository;

    public Log4NetLoggerProvider(string configFilePath)
    {
        _repository = LogManager.GetRepository(Assembly.GetEntryAssembly() ?? Assembly.GetExecutingAssembly());

        var configFile = new FileInfo(configFilePath);
        if (configFile.Exists)
        {
            XmlConfigurator.ConfigureAndWatch(_repository, configFile);
        }
        else
        {
            XmlConfigurator.Configure(_repository);
        }
    }

    public MsILogger CreateLogger(string categoryName)
    {
        return new Log4NetLogger(LogManager.GetLogger(_repository.Name, categoryName));
    }

    public void Dispose()
    {
        _repository.Shutdown();
    }

    private sealed class Log4NetLogger : MsILogger
    {
        private readonly ILog _logger;

        public Log4NetLogger(ILog logger)
        {
            _logger = logger;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(MsLogLevel logLevel)
        {
            return logLevel != MsLogLevel.None &&
                _logger.Logger.IsEnabledFor(ToLog4NetLevel(logLevel));
        }

        public void Log<TState>(
            MsLogLevel logLevel,
            MsEventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            if (string.IsNullOrEmpty(message) && exception is null)
            {
                return;
            }

            _logger.Logger.Log(typeof(Log4NetLogger), ToLog4NetLevel(logLevel), message, exception);
        }

        private static Log4NetLevel ToLog4NetLevel(MsLogLevel logLevel)
        {
            return logLevel switch
            {
                MsLogLevel.Trace => Log4NetLevel.Trace,
                MsLogLevel.Debug => Log4NetLevel.Debug,
                MsLogLevel.Information => Log4NetLevel.Info,
                MsLogLevel.Warning => Log4NetLevel.Warn,
                MsLogLevel.Error => Log4NetLevel.Error,
                MsLogLevel.Critical => Log4NetLevel.Fatal,
                _ => Log4NetLevel.Off
            };
        }
    }

    private sealed class NullScope : IDisposable
    {
        public static readonly NullScope Instance = new();

        public void Dispose()
        {
        }
    }
}
