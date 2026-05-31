using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using PrinterSecsGem.Eq.Hardware;
using PrinterSecsGem.Eq.Printing;
using PrinterSecsGem.Eq.Secs;
using Secs4Net;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureServices((context, services) =>
    {
        services.Configure<PrinterOptions>(context.Configuration.GetSection("Printer"));
        services.Configure<LabelTemplateOptions>(context.Configuration.GetSection("LabelTemplate"));
        services.Configure<MockHardwareOptions>(context.Configuration.GetSection("MockHardware"));

        services.AddSecs4Net<SecsGemLogger>(context.Configuration);

        services.AddSingleton<ZplLabelTemplate>();
        services.AddSingleton<IPrinterGateway, FilePrinterGateway>();
        services.AddSingleton<IHardwareGateway, MockHardwareGateway>();
        services.AddSingleton<SecsMessageDispatcher>();
        services.AddHostedService<SecsPrimaryMessageWorker>();
    });

await builder.Build().RunAsync();
