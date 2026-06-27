using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrinterSecsGem.Eq;
using PrinterSecsGem.Eq.ErackNetwork;
using PrinterSecsGem.Eq.Hardware;
using PrinterSecsGem.Eq.Hardware.ERack;
using PrinterSecsGem.Eq.Logging;
using PrinterSecsGem.Eq.Printing;
using PrinterSecsGem.Eq.Secs;
using PrinterSecsGem.Eq.StatusUi;
using PrinterSecsGem.Eq.Validation;
using Secs4Net;

var runLocalValidation = args.Any(arg => arg.Equals("--validate-local", StringComparison.OrdinalIgnoreCase));
var runSensorPollValidation = args.Any(arg => arg.Equals("--validate-sensor-poll", StringComparison.OrdinalIgnoreCase));
var runReadTagLocal = args.Any(arg => arg.Equals("--read-tag-local", StringComparison.OrdinalIgnoreCase));
var runWriteTagLocal = args.Any(arg => arg.Equals("--write-tag-local", StringComparison.OrdinalIgnoreCase));
var runSecs = args.Any(arg => arg.Equals("--secs", StringComparison.OrdinalIgnoreCase));
var runStatusUi = !runSecs && (!args.Any() || args.Any(arg => arg.Equals("--status-ui", StringComparison.OrdinalIgnoreCase)));
var hostArgs = args
    .Where(arg => !arg.Equals("--validate-local", StringComparison.OrdinalIgnoreCase))
    .Where(arg => !arg.Equals("--validate-sensor-poll", StringComparison.OrdinalIgnoreCase))
    .Where(arg => !arg.Equals("--read-tag-local", StringComparison.OrdinalIgnoreCase))
    .Where(arg => !arg.Equals("--write-tag-local", StringComparison.OrdinalIgnoreCase))
    .Where(arg => !arg.Equals("--secs", StringComparison.OrdinalIgnoreCase))
    .Where(arg => !arg.Equals("--status-ui", StringComparison.OrdinalIgnoreCase))
    .ToArray();

var builder = Host.CreateDefaultBuilder(hostArgs)
    .ConfigureAppConfiguration((context, configuration) =>
    {
        EnsureDefaultRuntimeConfigFiles(context.HostingEnvironment.ContentRootPath);

        configuration.Sources.Insert(0, new MemoryConfigurationSource
        {
            InitialData = GetDefaultConfigurationValues()
        });

        var appConfigValues = LoadAppConfigValues(context.HostingEnvironment.ContentRootPath);
        if (appConfigValues.Count > 0)
        {
            configuration.AddInMemoryCollection(appConfigValues);
        }
    })
    .ConfigureLogging((context, logging) =>
    {
        logging.ClearProviders();

        var configFile = context.Configuration["Log4Net:ConfigFile"] ?? "log4net.config";
        var configPath = ResolveConfigPath(context.HostingEnvironment.ContentRootPath, configFile);
        logging.AddProvider(new Log4NetLoggerProvider(configPath));
    })
    .ConfigureServices((context, services) =>
    {
        services.Configure<PrinterOptions>(context.Configuration.GetSection("Printer"));
        services.Configure<RuntimeOptions>(context.Configuration.GetSection("Runtime"));
        services.Configure<LabelTemplateOptions>(context.Configuration.GetSection("LabelTemplate"));
        services.Configure<MockHardwareOptions>(context.Configuration.GetSection("MockHardware"));
        services.Configure<ERackHardwareOptions>(context.Configuration.GetSection("ERackHardware"));
        services.Configure<ERackSensorDisplayOptions>(context.Configuration.GetSection("ERackSensorDisplay"));
        services.Configure<ERackServerOptions>(context.Configuration.GetSection("ERackServer"));
        services.Configure<ERackClientOptions>(context.Configuration.GetSection("ERackClient"));
        services.Configure<ERackSimulationOptions>(context.Configuration.GetSection("ERackSimulation"));
        services.Configure<LocalValidationOptions>(context.Configuration.GetSection("LocalValidation"));
        services.Configure<SecsEventOptions>(context.Configuration.GetSection("SecsEvents"));
        services.Configure<SecsGemOptions>(context.Configuration.GetSection("secs4net"));
        services.Configure<StatusUiOptions>(context.Configuration.GetSection("StatusUi"));
        services.Configure<HostOptions>(options => options.ShutdownTimeout = TimeSpan.FromSeconds(5));

        services.AddSingleton<AppConfigWriter>();
        services.AddSingleton<ZplLabelTemplate>();
        services.AddSingleton<FilePrinterGateway>();
        services.AddSingleton<ZebraCommandLinePrinterGateway>();
        services.AddSingleton<StatusUiEventBus>();
        services.AddSingleton<IPrinterGateway>(serviceProvider =>
        {
            var options = context.Configuration.GetSection("Printer").Get<PrinterOptions>() ?? new PrinterOptions();
            if (!options.RealPrintEnabled)
            {
                return serviceProvider.GetRequiredService<FilePrinterGateway>();
            }

            return options.Mode.Equals("ZebraCommandLine", StringComparison.OrdinalIgnoreCase)
                ? serviceProvider.GetRequiredService<ZebraCommandLinePrinterGateway>()
                : serviceProvider.GetRequiredService<FilePrinterGateway>();
        });
        services.AddSingleton<MockHardwareGateway>();
        services.AddSingleton<ERackLocationRegistry>();
        services.AddSingleton<ERackSerialHardwareGateway>();
        services.AddSingleton<IHardwareGateway>(serviceProvider =>
        {
            var options = context.Configuration.GetSection("ERackHardware").Get<ERackHardwareOptions>() ?? new ERackHardwareOptions();
            return options.Enabled
                ? serviceProvider.GetRequiredService<ERackSerialHardwareGateway>()
                : serviceProvider.GetRequiredService<MockHardwareGateway>();
        });
        services.AddSingleton<SecsEventMessageFactory>();
        services.AddSingleton<SecsEventPublisher>();
        services.AddSingleton<ISecsGemLogger, SecsGemLogger>();
        services.AddSingleton<ISecsConnection, HsmsConnection>();
        services.AddSingleton<ISecsGem, SecsGem>();
        services.AddSingleton<SecsMessageDispatcher>();
        services.AddSingleton<ERackTcpUnitRouter>();
        services.AddSingleton<IERackUnitRouter>(serviceProvider => serviceProvider.GetRequiredService<ERackTcpUnitRouter>());
        services.AddSingleton<ERackClientWorker>();
        services.AddSingleton<IERackEventSink, ERackEventSink>();
        services.AddSingleton<StatusDashboardForm>();

        if (runLocalValidation)
        {
            services.AddHostedService<LocalValidationWorker>();
        }
        else if (runSensorPollValidation)
        {
            services.AddHostedService<SensorPollingValidationWorker>();
        }
        else if (runReadTagLocal)
        {
            services.AddHostedService<ReadTagValidationWorker>();
        }
        else if (runWriteTagLocal)
        {
            services.AddHostedService<WriteTagValidationWorker>();
        }
        else
        {
            var runtimeOptions = context.Configuration.GetSection("Runtime").Get<RuntimeOptions>() ?? new RuntimeOptions();
            var serverOptions = context.Configuration.GetSection("ERackServer").Get<ERackServerOptions>() ?? new ERackServerOptions();
            var clientOptions = context.Configuration.GetSection("ERackClient").Get<ERackClientOptions>() ?? new ERackClientOptions();
            var simulationOptions = context.Configuration.GetSection("ERackSimulation").Get<ERackSimulationOptions>() ?? new ERackSimulationOptions();
            var startUnitClient = runtimeOptions.IsUnitEnabled && (clientOptions.Enabled || runtimeOptions.IsServerEnabled);
            var startSecs = runtimeOptions.IsServerEnabled || !clientOptions.Enabled;

            if (startSecs)
            {
                services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<SecsEventPublisher>());
                services.AddHostedService<SecsPrimaryMessageWorker>();
            }

            if (runtimeOptions.IsUnitEnabled)
            {
                services.AddHostedService<ERackSensorDisplayWorker>();
            }

            if (runtimeOptions.IsServerEnabled && serverOptions.Enabled)
            {
                services.AddHostedService<ERackServerWorker>();
            }

            if (startUnitClient)
            {
                services.AddHostedService(serviceProvider => serviceProvider.GetRequiredService<ERackClientWorker>());
            }

            if (runtimeOptions.IsUnitEnabled && simulationOptions.Enabled)
            {
                services.AddHostedService<ERackSimulationWorker>();
            }
        }
    });

var app = builder.Build();
LogEffectiveConfiguration(app.Services);
var startupLogger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("PrinterSecsGem.Eq.Startup");
try
{
    if (runStatusUi)
    {
        ApplicationConfiguration.Initialize();
        var erackGateway = app.Services.GetRequiredService<ERackSerialHardwareGateway>();
        var statusForm = app.Services.GetRequiredService<StatusDashboardForm>();
        var appStarted = false;
        Application.ApplicationExit += (_, _) => erackGateway.BeginShutdown();

        try
        {
            await app.StartAsync();
            appStarted = true;
            Application.Run(statusForm);
        }
        finally
        {
            erackGateway.BeginShutdown();
            if (appStarted)
            {
                using var stopTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                try
                {
                    await app.StopAsync(stopTimeout.Token);
                }
                catch (OperationCanceledException)
                {
                    startupLogger.LogWarning("Host shutdown timed out after 5 seconds.");
                }
            }

            app.Dispose();
        }

        return;
    }

    await app.RunAsync();
}
catch (Exception ex)
{
    startupLogger.LogError(ex, "PrinterSecsGem.Eq failed during startup or runtime.");
    if (runStatusUi)
    {
        try
        {
            MessageBox.Show(
                $"程序启动失败：{ex.Message}{Environment.NewLine}{Environment.NewLine}请查看 logs\\printer-secs-gem.log。",
                "打印机 SECS/GEM 启动失败",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
        catch
        {
            // If Windows cannot show a message box, the log entry above is still the source of truth.
        }
    }

    throw;
}

static void LogEffectiveConfiguration(IServiceProvider services)
{
    var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("PrinterSecsGem.Eq.Startup");
    var secsOptions = services.GetRequiredService<IOptions<SecsGemOptions>>().Value;
    var runtimeOptions = services.GetRequiredService<IOptions<RuntimeOptions>>().Value;
    var printerOptions = services.GetRequiredService<IOptions<PrinterOptions>>().Value;
    var erackOptions = services.GetRequiredService<IOptions<ERackHardwareOptions>>().Value;
    var sensorDisplayOptions = services.GetRequiredService<IOptions<ERackSensorDisplayOptions>>().Value;
    var erackServerOptions = services.GetRequiredService<IOptions<ERackServerOptions>>().Value;
    var erackClientOptions = services.GetRequiredService<IOptions<ERackClientOptions>>().Value;
    var simulationOptions = services.GetRequiredService<IOptions<ERackSimulationOptions>>().Value;

    logger.LogInformation(
        "Effective runtime config: mode={RuntimeMode}, unitEnabled={UnitEnabled}, serverEnabled={ServerEnabled}",
        runtimeOptions.NormalizedMode,
        runtimeOptions.IsUnitEnabled,
        runtimeOptions.IsServerEnabled);
    logger.LogInformation(
        "Effective SECS config: deviceId={DeviceId}, isActive={IsActive}, ipAddress={IpAddress}, port={Port}",
        secsOptions.DeviceId,
        secsOptions.IsActive,
        secsOptions.IpAddress,
        secsOptions.Port);
    logger.LogInformation(
        "Effective printer config: realPrintEnabled={RealPrintEnabled}, mode={Mode}, outputDirectory={OutputDirectory}, zebraPreflightStatusEnabled={ZebraPreflightStatusEnabled}, zebraCommandTimeoutMs={ZebraCommandTimeoutMilliseconds}",
        printerOptions.RealPrintEnabled,
        printerOptions.Mode,
        printerOptions.OutputDirectory,
        printerOptions.ZebraPreflightStatusEnabled,
        printerOptions.ZebraCommandTimeoutMilliseconds);
    logger.LogInformation(
        "Effective ERack config: enabled={Enabled}, portName={PortName}, baudRate={BaudRate}, deviceAddress={DeviceAddress}, inventoryMode={InventoryMode}, keepPortOpen={KeepPortOpen}, writeTagStartPage={WriteTagStartPage}",
        erackOptions.Enabled,
        erackOptions.PortName,
        erackOptions.BaudRate,
        erackOptions.DeviceAddress,
        erackOptions.InventoryMode,
        erackOptions.KeepPortOpen,
        erackOptions.WriteTagStartPage);
    logger.LogInformation(
        "Effective ERack sensor/display config: enabled={Enabled}, pollIntervalMs={PollIntervalMilliseconds}, sensorCommand=0x{SensorCommand:X2}, sensorPayloadIndex={SensorPayloadIndex}, checkLevel={CheckLevel}",
        sensorDisplayOptions.Enabled,
        sensorDisplayOptions.PollIntervalMilliseconds,
        sensorDisplayOptions.SensorCommand,
        sensorDisplayOptions.SensorPayloadIndex,
        sensorDisplayOptions.CheckLevel);
    logger.LogInformation(
        "Effective ERACK Server config: enabled={Enabled}, listen={ListenIp}:{Port}, requestTimeoutMs={RequestTimeoutMilliseconds}",
        erackServerOptions.Enabled,
        erackServerOptions.ListenIp,
        erackServerOptions.Port,
        erackServerOptions.RequestTimeoutMilliseconds);
    logger.LogInformation(
        "Effective ERACK Client config: enabled={Enabled}, server={ServerHost}:{ServerPort}, unitId={UnitId}, shelfId={ShelfId}, reconnectDelayMs={ReconnectDelayMilliseconds}",
        erackClientOptions.Enabled,
        erackClientOptions.ServerHost,
        erackClientOptions.ServerPort,
        erackClientOptions.UnitId,
        erackClientOptions.ShelfId,
        erackClientOptions.ReconnectDelayMilliseconds);
    logger.LogInformation(
        "Effective ERACK simulation config: enabled={Enabled}, shelf={ShelfId}, location={LocationId}, tag={Tag}, startupDelayMs={StartupDelayMilliseconds}, stepIntervalMs={StepIntervalMilliseconds}, loop={Loop}",
        simulationOptions.Enabled,
        simulationOptions.ShelfId,
        simulationOptions.LocationId,
        simulationOptions.Tag,
        simulationOptions.StartupDelayMilliseconds,
        simulationOptions.StepIntervalMilliseconds,
        simulationOptions.Loop);
}

static string ResolveConfigPath(string contentRootPath, string configFile)
{
    if (Path.IsPathRooted(configFile))
    {
        return configFile;
    }

    var contentRootConfig = Path.Combine(contentRootPath, configFile);
    if (File.Exists(contentRootConfig))
    {
        return contentRootConfig;
    }

    return Path.Combine(AppContext.BaseDirectory, configFile);
}

static void EnsureDefaultRuntimeConfigFiles(string contentRootPath)
{
    EnsureDefaultAppConfigExists(contentRootPath);
    EnsureDefaultLog4NetConfigExists(contentRootPath);
}

static void EnsureDefaultAppConfigExists(string contentRootPath)
{
    if (GetAppConfigPaths(contentRootPath).Any(File.Exists))
    {
        return;
    }

    var configPath = Path.Combine(AppContext.BaseDirectory, "App.config");
    try
    {
        Directory.CreateDirectory(Path.GetDirectoryName(configPath) ?? AppContext.BaseDirectory);
        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(
                "configuration",
                new XElement(
                    "appSettings",
                    GetDefaultConfigurationValues().Select(item =>
                        new XElement(
                            "add",
                            new XAttribute("key", item.Key),
                            new XAttribute("value", item.Value ?? string.Empty))))));

        XmlConfigFile.Save(document, configPath);
    }
    catch
    {
        // The application can still run from embedded defaults if the target folder is read-only.
    }
}

static void EnsureDefaultLog4NetConfigExists(string contentRootPath)
{
    var configuredPath = ResolveConfigPath(contentRootPath, "log4net.config");
    if (File.Exists(configuredPath))
    {
        return;
    }

    var configPath = Path.Combine(AppContext.BaseDirectory, "log4net.config");
    try
    {
        Directory.CreateDirectory(Path.GetDirectoryName(configPath) ?? AppContext.BaseDirectory);
        File.WriteAllText(
            configPath,
            """
            <?xml version="1.0" encoding="utf-8" ?>
            <log4net>
              <appender name="RollingFile" type="log4net.Appender.RollingFileAppender">
                <file value="logs/printer-secs-gem.log" />
                <appendToFile value="true" />
                <rollingStyle value="Date" />
                <datePattern value="'.'yyyyMMdd" />
                <staticLogFileName value="true" />
                <layout type="log4net.Layout.PatternLayout">
                  <conversionPattern value="%date %-5level [%thread] %logger - %message%newline%exception" />
                </layout>
              </appender>

              <appender name="Console" type="log4net.Appender.ConsoleAppender">
                <layout type="log4net.Layout.PatternLayout">
                  <conversionPattern value="%date %-5level %logger - %message%newline%exception" />
                </layout>
              </appender>

              <root>
                <level value="INFO" />
                <appender-ref ref="RollingFile" />
                <appender-ref ref="Console" />
              </root>
            </log4net>
            """);
    }
    catch
    {
        // Logging falls back to log4net defaults if the target folder is read-only.
    }
}

static Dictionary<string, string?> LoadAppConfigValues(string contentRootPath)
{
    var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

    foreach (var configPath in GetAppConfigPaths(contentRootPath).Distinct(StringComparer.OrdinalIgnoreCase))
    {
        if (!File.Exists(configPath))
        {
            continue;
        }

        foreach (var item in ReadAppSettings(configPath))
        {
            values[item.Key] = item.Value;
        }
    }

    return values;
}

static IEnumerable<KeyValuePair<string, string?>> GetDefaultConfigurationValues()
{
    yield return new("Log4Net:ConfigFile", "log4net.config");
    yield return new("StatusUi:Language", "zh-CN");
    yield return new("Runtime:Mode", "Unit");

    yield return new("ERackServer:Enabled", "true");
    yield return new("ERackServer:ListenIp", "127.0.0.1");
    yield return new("ERackServer:Port", "7801");
    yield return new("ERackServer:RequestTimeoutMilliseconds", "60000");

    yield return new("ERackClient:Enabled", "false");
    yield return new("ERackClient:ServerHost", "127.0.0.1");
    yield return new("ERackClient:ServerPort", "7801");
    yield return new("ERackClient:UnitId", "UNIT001");
    yield return new("ERackClient:ShelfId", "SHELF001");
    yield return new("ERackClient:ReconnectDelayMilliseconds", "3000");
    yield return new("ERackClient:HeartbeatIntervalMilliseconds", "5000");

    yield return new("ERackSimulation:Enabled", "false");
    yield return new("ERackSimulation:ShelfId", "SHELF001");
    yield return new("ERackSimulation:LocationId", "LOC001");
    yield return new("ERackSimulation:Tag", "RFID1234567890");
    yield return new("ERackSimulation:StartupDelayMilliseconds", "3000");
    yield return new("ERackSimulation:StepIntervalMilliseconds", "5000");
    yield return new("ERackSimulation:Loop", "true");

    yield return new("secs4net:IpAddress", "127.0.0.1");
    yield return new("secs4net:Port", "5000");
    yield return new("secs4net:DeviceId", "1");
    yield return new("secs4net:IsActive", "true");
    yield return new("secs4net:T3", "45000");
    yield return new("secs4net:T5", "10000");
    yield return new("secs4net:T6", "5000");
    yield return new("secs4net:T7", "10000");
    yield return new("secs4net:T8", "5000");
    yield return new("secs4net:LinkTestInterval", "60000");

    yield return new("SecsEvents:InitialDataId", "1");
    yield return new("SecsEvents:ActiveReportsEnabled", "true");
    yield return new("SecsEvents:ActiveEventReplyExpected", "false");
    yield return new("SecsEvents:TagReadCeid", "1001");
    yield return new("SecsEvents:TagReadRptid", "2001");
    yield return new("SecsEvents:RfidWriteCeid", "2002");
    yield return new("SecsEvents:RfidWriteRptid", "2002");
    yield return new("SecsEvents:PrintCompletedCeid", "2003");
    yield return new("SecsEvents:PrintFailedCeid", "2004");
    yield return new("SecsEvents:PrintRptid", "2003");
    yield return new("SecsEvents:ShelfIdVid", "3001");
    yield return new("SecsEvents:LocationIdVid", "3002");
    yield return new("SecsEvents:TagVid", "3003");
    yield return new("SecsEvents:IsLoadedVid", "3004");
    yield return new("SecsEvents:TimestampVid", "3005");

    yield return new("Printer:RealPrintEnabled", "true");
    yield return new("Printer:Mode", "ZebraCommandLine");
    yield return new("Printer:OutputDirectory", "output/zpl");
    yield return new("Printer:DefaultPrinterId", "PRINTER001");
    yield return new("Printer:ZebraCommandLineAssembly", "zebra-command-line/SdkApi.Desktop.CommandLine.dll");
    yield return new("Printer:ZebraConnectionType", "Usb");
    yield return new("Printer:ZebraPrinterAddress", "");
    yield return new("Printer:ZebraPreflightStatusEnabled", "false");
    yield return new("Printer:ZebraCommandTimeoutMilliseconds", "10000");
    yield return new("Printer:DotnetExecutable", "dotnet");

    yield return new("LabelTemplate:UseMinimalCompatibleCommands", "false");
    yield return new("LabelTemplate:ResetPrinterState", "false");
    yield return new("LabelTemplate:Orientation", "Normal");
    yield return new("LabelTemplate:PrintOrientation", "N");
    yield return new("LabelTemplate:Dpi", "203");
    yield return new("LabelTemplate:PrintDarkness", "0");
    yield return new("LabelTemplate:WidthDots", "480");
    yield return new("LabelTemplate:HeightDots", "320");
    yield return new("LabelTemplate:LabelLengthAppliesToAllMedia", "false");
    yield return new("LabelTemplate:LabelTop", "0");
    yield return new("LabelTemplate:LabelShift", "0");
    yield return new("LabelTemplate:TearOffAdjust", "0");
    yield return new("LabelTemplate:LabelHomeX", "0");
    yield return new("LabelTemplate:LabelHomeY", "0");
    yield return new("LabelTemplate:TopTextX", "55");
    yield return new("LabelTemplate:TopTextY", "35");
    yield return new("LabelTemplate:TopTextSize", "40");
    yield return new("LabelTemplate:TopTextHeight", "70");
    yield return new("LabelTemplate:TopTextWidth", "55");
    yield return new("LabelTemplate:TopTextBlockWidth", "370");
    yield return new("LabelTemplate:BarcodeX", "75");
    yield return new("LabelTemplate:BarcodeY", "95");
    yield return new("LabelTemplate:BarcodeModuleWidth", "2");
    yield return new("LabelTemplate:BarcodeHeight", "80");
    yield return new("LabelTemplate:BarcodeHumanReadable", "false");
    yield return new("LabelTemplate:BarcodeHumanReadableAbove", "false");
    yield return new("LabelTemplate:BarcodePrintCheckDigit", "false");
    yield return new("LabelTemplate:BarcodeTextEnabled", "true");
    yield return new("LabelTemplate:BarcodeTextX", "120");
    yield return new("LabelTemplate:BarcodeTextY", "190");
    yield return new("LabelTemplate:BarcodeTextFont", "0");
    yield return new("LabelTemplate:BarcodeTextRenderMode", "ZplFont");
    yield return new("LabelTemplate:BarcodeTextBitmapFontFamily", "Arial");
    yield return new("LabelTemplate:BarcodeTextBitmapFontSize", "0");
    yield return new("LabelTemplate:BarcodeTextBitmapThreshold", "150");
    yield return new("LabelTemplate:BarcodeTextSize", "22");
    yield return new("LabelTemplate:BarcodeTextHeight", "38");
    yield return new("LabelTemplate:BarcodeTextWidth", "34");

    yield return new("MockHardware:DefaultShelfId", "SHELF001");
    yield return new("MockHardware:DefaultLocationId", "LOC001");
    yield return new("MockHardware:DefaultTag", "EFS08IZS");

    yield return new("ERackHardware:Enabled", "true");
    yield return new("ERackHardware:PortName", "COM11");
    yield return new("ERackHardware:BaudRate", "57600");
    yield return new("ERackHardware:DeviceAddress", "1");
    yield return new("ERackHardware:InventoryMode", "4");
    yield return new("ERackHardware:InventoryWaitTimeMilliseconds", "50");
    yield return new("ERackHardware:InventoryWaitCount", "20");
    yield return new("ERackHardware:KeepPortOpen", "true");
    yield return new("ERackHardware:WriteTagStartPage", "1");
    yield return new("ERackHardware:WriteTagWaitTimeMilliseconds", "200");
    yield return new("ERackHardware:WriteTagWaitCount", "20");
    yield return new("ERackHardware:DefaultShelfId", "SHELF001");
    yield return new("ERackHardware:DefaultLocationId", "LOC001");

    yield return new("ERackSensorDisplay:Enabled", "false");
    yield return new("ERackSensorDisplay:PollIntervalMilliseconds", "500");
    yield return new("ERackSensorDisplay:SensorCommand", "5");
    yield return new("ERackSensorDisplay:SensorPayloadIndex", "1");
    yield return new("ERackSensorDisplay:CheckLevel", "0");
    yield return new("ERackSensorDisplay:SensorWaitTimeMilliseconds", "5");
    yield return new("ERackSensorDisplay:SensorWaitCount", "6");
    yield return new("ERackSensorDisplay:DisplayWaitTimeMilliseconds", "20");
    yield return new("ERackSensorDisplay:DisplayMinWaitCount", "5");
    yield return new("ERackSensorDisplay:DisplayMaxBytes", "512");
    yield return new("ERackSensorDisplay:ReadLengthBytes", "32");
    yield return new("ERackSensorDisplay:NoIdDisplayText", "NO ID  ");
    yield return new("ERackSensorDisplay:NoIdFailureCode", "2001");
    yield return new("ERackSensorDisplay:UpdateDisplayOnEveryPoll", "false");

    yield return new("Locations:LOC001:ShelfId", "SHELF001");
    yield return new("Locations:LOC001:BaudRate", "57600");
    yield return new("Locations:LOC001:DeviceAddress", "1");

    yield return new("LocalValidation:ShelfId", "SHELF001");
    yield return new("LocalValidation:LocationId", "LOC001");
    yield return new("LocalValidation:PrinterId", "PRINTER001");
    yield return new("LocalValidation:Content", "EFS08IZS");
    yield return new("LocalValidation:Copies", "1");
}

static IEnumerable<string> GetAppConfigPaths(string contentRootPath)
{
    yield return Path.Combine(AppContext.BaseDirectory, "PrinterSecsGem.Eq.dll.config");
    yield return Path.Combine(AppContext.BaseDirectory, "PrinterSecsGem.Eq.exe.config");
    yield return Path.Combine(contentRootPath, "PrinterSecsGem.Eq.dll.config");
    yield return Path.Combine(contentRootPath, "PrinterSecsGem.Eq.exe.config");
    yield return Path.Combine(AppContext.BaseDirectory, "App.config");
    yield return Path.Combine(contentRootPath, "App.config");
}

static IEnumerable<KeyValuePair<string, string?>> ReadAppSettings(string configPath)
{
    var document = XDocument.Load(configPath);
    var appSettings = document.Root?.Element("appSettings");
    if (appSettings is null)
    {
        yield break;
    }

    foreach (var add in appSettings.Elements("add"))
    {
        var key = add.Attribute("key")?.Value;
        if (string.IsNullOrWhiteSpace(key))
        {
            continue;
        }

        yield return new KeyValuePair<string, string?>(key, add.Attribute("value")?.Value);
    }
}
