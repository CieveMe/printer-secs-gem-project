using System.Xml.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PrinterSecsGem.Eq.Hardware;
using PrinterSecsGem.Eq.Hardware.ERack;
using PrinterSecsGem.Eq.Logging;
using PrinterSecsGem.Eq.Printing;
using PrinterSecsGem.Eq.Secs;
using PrinterSecsGem.Eq.StatusUi;
using PrinterSecsGem.Eq.Validation;
using Secs4Net;

var runLocalValidation = args.Any(arg => arg.Equals("--validate-local", StringComparison.OrdinalIgnoreCase));
var runReadTagLocal = args.Any(arg => arg.Equals("--read-tag-local", StringComparison.OrdinalIgnoreCase));
var runWriteTagLocal = args.Any(arg => arg.Equals("--write-tag-local", StringComparison.OrdinalIgnoreCase));
var runSecs = args.Any(arg => arg.Equals("--secs", StringComparison.OrdinalIgnoreCase));
var runStatusUi = !runSecs && (!args.Any() || args.Any(arg => arg.Equals("--status-ui", StringComparison.OrdinalIgnoreCase)));
var hostArgs = args
    .Where(arg => !arg.Equals("--validate-local", StringComparison.OrdinalIgnoreCase))
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
        services.Configure<LabelTemplateOptions>(context.Configuration.GetSection("LabelTemplate"));
        services.Configure<MockHardwareOptions>(context.Configuration.GetSection("MockHardware"));
        services.Configure<ERackHardwareOptions>(context.Configuration.GetSection("ERackHardware"));
        services.Configure<LocalValidationOptions>(context.Configuration.GetSection("LocalValidation"));
        services.Configure<SecsEventOptions>(context.Configuration.GetSection("SecsEvents"));
        services.Configure<SecsGemOptions>(context.Configuration.GetSection("secs4net"));

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
        services.AddSingleton<ERackSerialHardwareGateway>();
        services.AddSingleton<IHardwareGateway>(serviceProvider =>
        {
            var options = context.Configuration.GetSection("ERackHardware").Get<ERackHardwareOptions>() ?? new ERackHardwareOptions();
            return options.Enabled
                ? serviceProvider.GetRequiredService<ERackSerialHardwareGateway>()
                : serviceProvider.GetRequiredService<MockHardwareGateway>();
        });
        services.AddSingleton<SecsEventMessageFactory>();
        services.AddSingleton<StatusDashboardForm>();

        if (runLocalValidation)
        {
            services.AddHostedService<LocalValidationWorker>();
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
            services.AddSingleton<ISecsGemLogger, SecsGemLogger>();
            services.AddSingleton<ISecsConnection, HsmsConnection>();
            services.AddSingleton<ISecsGem, SecsGem>();
            services.AddSingleton<SecsMessageDispatcher>();
            services.AddHostedService<SecsPrimaryMessageWorker>();
        }
    });

var app = builder.Build();
LogEffectiveConfiguration(app.Services);
if (runStatusUi)
{
    ApplicationConfiguration.Initialize();
    var erackGateway = app.Services.GetRequiredService<ERackSerialHardwareGateway>();
    var statusForm = app.Services.GetRequiredService<StatusDashboardForm>();
    Application.ApplicationExit += (_, _) => erackGateway.ClosePort();

    try
    {
        await app.StartAsync();
        Application.Run(statusForm);
    }
    finally
    {
        erackGateway.ClosePort();
        await app.StopAsync();
        app.Dispose();
    }

    return;
}

await app.RunAsync();

static void LogEffectiveConfiguration(IServiceProvider services)
{
    var logger = services.GetRequiredService<ILoggerFactory>().CreateLogger("PrinterSecsGem.Eq.Startup");
    var secsOptions = services.GetRequiredService<IOptions<SecsGemOptions>>().Value;
    var printerOptions = services.GetRequiredService<IOptions<PrinterOptions>>().Value;
    var erackOptions = services.GetRequiredService<IOptions<ERackHardwareOptions>>().Value;

    logger.LogInformation(
        "Effective SECS config: deviceId={DeviceId}, isActive={IsActive}, ipAddress={IpAddress}, port={Port}",
        secsOptions.DeviceId,
        secsOptions.IsActive,
        secsOptions.IpAddress,
        secsOptions.Port);
    logger.LogInformation(
        "Effective printer config: realPrintEnabled={RealPrintEnabled}, mode={Mode}, outputDirectory={OutputDirectory}",
        printerOptions.RealPrintEnabled,
        printerOptions.Mode,
        printerOptions.OutputDirectory);
    logger.LogInformation(
        "Effective ERack config: enabled={Enabled}, portName={PortName}, baudRate={BaudRate}, deviceAddress={DeviceAddress}, inventoryMode={InventoryMode}, keepPortOpen={KeepPortOpen}, writeTagStartPage={WriteTagStartPage}",
        erackOptions.Enabled,
        erackOptions.PortName,
        erackOptions.BaudRate,
        erackOptions.DeviceAddress,
        erackOptions.InventoryMode,
        erackOptions.KeepPortOpen,
        erackOptions.WriteTagStartPage);
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

        document.Save(configPath);
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
    yield return new("SecsEvents:TagReadCeid", "1001");
    yield return new("SecsEvents:TagReadRptid", "2001");
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
    yield return new("Printer:DotnetExecutable", "dotnet");

    yield return new("LabelTemplate:ResetPrinterState", "true");
    yield return new("LabelTemplate:Orientation", "Normal");
    yield return new("LabelTemplate:PrintOrientation", "N");
    yield return new("LabelTemplate:Dpi", "203");
    yield return new("LabelTemplate:WidthDots", "508");
    yield return new("LabelTemplate:HeightDots", "320");
    yield return new("LabelTemplate:LabelLengthAppliesToAllMedia", "true");
    yield return new("LabelTemplate:LabelTop", "0");
    yield return new("LabelTemplate:LabelShift", "0");
    yield return new("LabelTemplate:TearOffAdjust", "0");
    yield return new("LabelTemplate:LabelHomeX", "0");
    yield return new("LabelTemplate:LabelHomeY", "0");
    yield return new("LabelTemplate:TopTextX", "0");
    yield return new("LabelTemplate:TopTextY", "42");
    yield return new("LabelTemplate:TopTextHeight", "96");
    yield return new("LabelTemplate:TopTextWidth", "88");
    yield return new("LabelTemplate:TopTextBlockWidth", "508");
    yield return new("LabelTemplate:BarcodeX", "66");
    yield return new("LabelTemplate:BarcodeY", "156");
    yield return new("LabelTemplate:BarcodeModuleWidth", "3");
    yield return new("LabelTemplate:BarcodeHeight", "90");
    yield return new("LabelTemplate:BarcodeHumanReadable", "true");
    yield return new("LabelTemplate:BarcodeHumanReadableAbove", "false");
    yield return new("LabelTemplate:BarcodePrintCheckDigit", "false");

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
