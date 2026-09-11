using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using PrinterSecsGem.Eq;
using PrinterSecsGem.Eq.ErackNetwork;
using PrinterSecsGem.Eq.Hardware;
using PrinterSecsGem.Eq.Hardware.ERack;
using PrinterSecsGem.Eq.Models;
using PrinterSecsGem.Eq.Printing;
using PrinterSecsGem.Eq.Secs;
using PrinterSecsGem.Eq.StatusUi;
using Secs4Net;
using static Secs4Net.Item;

var tests = new (string Name, Func<Task> Run)[]
{
    ("S1F2 returns ERACK and real version", TestS1F2Async),
    ("S1F14 returns COMMACK structure", TestS1F14Async),
    ("RFID MES output keeps only ASCII alphanumeric characters", TestRfidMesValueFilter),
    ("S10F3 preserves direct display spaces", TestDisplaySuccessAsync),
    ("S10F3 explicit empty clears display", TestDisplayClearAsync),
    ("S10F3 missing location maps to code 1", TestDisplayLocationNotFoundAsync),
    ("S10F3 malformed item maps to code 2", TestDisplayMalformedAsync),
    ("S10F3 oversized content maps to code 2", TestDisplayOversizedAsync),
    ("S10F3 server mode routes to remote unit", TestDisplayRemoteRouteAsync),
    ("Existing S5F12 S8F4 and S10F12 reply shapes remain", TestExistingReplyShapesAsync),
    ("RFID write event CEIDs remain 2002 and 2005", TestRfidWriteEventCeids),
    ("Shelf state event remains S6F21", TestShelfStateEvent),
    ("Sensor write verifies exact prefix and trims display only", TestSensorWriteSuccessAsync),
    ("Sensor write mismatch fails without display", TestSensorWriteMismatchAsync),
    ("Sensor write read failure returns verify failure", TestSensorWriteReadFailureAsync),
    ("Sensor display failure keeps write success", TestSensorDisplayFailureAsync),
    ("RFID polling write skips immediate read and display", TestRfidPollingSkipsVerifyAsync)
};

foreach (var test in tests)
{
    await test.Run();
    Console.WriteLine($"PASS {test.Name}");
}

Console.WriteLine($"All {tests.Length} protocol validations passed.");
return;

static async Task TestS1F2Async()
{
    var fixture = CreateFixture();
    using var request = new SecsMessage(1, 1) { SecsItem = L() };
    using var reply = await fixture.Dispatcher.DispatchAsync(request, CancellationToken.None);

    Equal((byte)1, reply!.S, "S1F2 stream");
    Equal((byte)2, reply.F, "S1F2 function");
    Equal(2, reply.SecsItem!.Count, "S1F2 root count");
    Equal("ERACK", reply.SecsItem[0].GetString(), "S1F2 equipment model");
    Equal("v1.0.8", reply.SecsItem[1].GetString(), "S1F2 software version");
}

static async Task TestS1F14Async()
{
    var fixture = CreateFixture();
    using var request = new SecsMessage(1, 13) { SecsItem = L() };
    using var reply = await fixture.Dispatcher.DispatchAsync(request, CancellationToken.None);

    Equal((byte)14, reply!.F, "S1F14 function");
    Equal(2, reply.SecsItem!.Count, "S1F14 root count");
    Equal(SecsFormat.Binary, reply.SecsItem[0].Format, "S1F14 COMMACK format");
    Equal((byte)0, reply.SecsItem[0].FirstValue<byte>(), "S1F14 COMMACK value");
    Equal(2, reply.SecsItem[1].Count, "S1F14 communication data count");
    Equal("ERACK", reply.SecsItem[1][0].GetString(), "S1F14 equipment model");
    Equal("v1.0.8", reply.SecsItem[1][1].GetString(), "S1F14 software version");
}

static Task TestRfidMesValueFilter()
{
    Equal("Abc123XyZ789", RfidMesValueFilter.Filter("Abc123_测试!@#XyZ789"), "mixed RFID filter");
    Equal("ABC123", RfidMesValueFilter.Filter("ABC 123"), "space RFID filter");
    Equal(string.Empty, RfidMesValueFilter.Filter("测试!@#"), "empty filtered RFID");
    Equal("TEST123", RfidMesValueFilter.Filter("TEST123   "), "trailing space RFID filter");
    return Task.CompletedTask;
}

static async Task TestDisplaySuccessAsync()
{
    var fixture = CreateFixture();
    using var request = DisplayRequest(A("ABC  "));
    using var reply = await fixture.Dispatcher.DispatchAsync(request, CancellationToken.None);

    AssertDisplayReply(reply, 0, "Set Success");
    Equal("ABC  ", fixture.Hardware.LastDisplay?.Content, "direct display content");
}

static async Task TestDisplayClearAsync()
{
    var fixture = CreateFixture();
    using var request = DisplayRequest(A());
    using var reply = await fixture.Dispatcher.DispatchAsync(request, CancellationToken.None);

    AssertDisplayReply(reply, 0, "Set Success");
    Equal(string.Empty, fixture.Hardware.LastDisplay?.Content, "clear display content");
}

static async Task TestDisplayLocationNotFoundAsync()
{
    var fixture = CreateFixture();
    fixture.Hardware.DisplayResult = OperationResult.Fail(6, "location not configured: LOC404");
    using var request = new SecsMessage(10, 3)
    {
        SecsItem = L(A("SHELF001"), A("LOC404"), A("ABC"))
    };
    using var reply = await fixture.Dispatcher.DispatchAsync(request, CancellationToken.None);

    AssertDisplayReply(reply, 1, "Location Not Found");
}

static async Task TestDisplayMalformedAsync()
{
    var fixture = CreateFixture();
    using var request = DisplayRequest(U1(1));
    using var reply = await fixture.Dispatcher.DispatchAsync(request, CancellationToken.None);

    AssertDisplayReply(reply, 2, "Display Content Format Error");
    Equal(0, fixture.Hardware.DisplayCount, "malformed display hardware call count");
}

static async Task TestDisplayOversizedAsync()
{
    var fixture = CreateFixture(displayMaxBytes: 4);
    using var request = DisplayRequest(A("12345"));
    using var reply = await fixture.Dispatcher.DispatchAsync(request, CancellationToken.None);

    AssertDisplayReply(reply, 2, "Display Content Format Error");
    Equal(0, fixture.Hardware.DisplayCount, "oversized display hardware call count");
}

static async Task TestDisplayRemoteRouteAsync()
{
    var fixture = CreateFixture(runtimeMode: "Server");
    using var request = DisplayRequest(A("REMOTE"));
    using var reply = await fixture.Dispatcher.DispatchAsync(request, CancellationToken.None);

    AssertDisplayReply(reply, 0, "Set Success");
    Equal("REMOTE", fixture.Router.LastDisplay?.Content, "remote display content");
    Equal(0, fixture.Hardware.DisplayCount, "local display count in server mode");
}

static async Task TestExistingReplyShapesAsync()
{
    var fixture = CreateFixture();
    using var shelfRequest = new SecsMessage(5, 11)
    {
        SecsItem = L(A("SHELF001"), A("LOC001"), U1(32))
    };
    using var shelfReply = await fixture.Dispatcher.DispatchAsync(shelfRequest, CancellationToken.None);
    Equal((byte)12, shelfReply!.F, "S5F12 function");
    Equal(4, shelfReply.SecsItem!.Count, "S5F12 root count");
    Equal((byte)0, shelfReply.SecsItem[2].FirstValue<byte>(), "S5F12 result code");
    Equal("Read Success", shelfReply.SecsItem[3].GetString(), "S5F12 description");

    using var printRequest = new SecsMessage(8, 3)
    {
        SecsItem = L(A("SHELF001"), A("PRINTER001"), A("PRINT"), U1(1))
    };
    using var printReply = await fixture.Dispatcher.DispatchAsync(printRequest, CancellationToken.None);
    Equal((byte)4, printReply!.F, "S8F4 function");
    Equal(4, printReply.SecsItem!.Count, "S8F4 root count");
    Equal((byte)0, printReply.SecsItem[2].FirstValue<byte>(), "S8F4 result code");

    using var writeRequest = new SecsMessage(10, 11)
    {
        SecsItem = L(A("SHELF001"), A("LOC001"), A("ABC"))
    };
    using var writeReply = await fixture.Dispatcher.DispatchAsync(writeRequest, CancellationToken.None);
    Equal((byte)12, writeReply!.F, "S10F12 function");
    Equal(4, writeReply.SecsItem!.Count, "S10F12 root count");
    Equal((byte)0, writeReply.SecsItem[2].FirstValue<byte>(), "S10F12 result code");
    Equal("Write Success", writeReply.SecsItem[3].GetString(), "S10F12 description");
}

static Task TestRfidWriteEventCeids()
{
    var factory = new SecsEventMessageFactory(Options.Create(new SecsEventOptions()));
    using var success = factory.CreateRfidWriteEvent(new RfidWriteEvent(
        "SHELF001",
        "LOC001",
        "ABC",
        0,
        "Write Success",
        DateTimeOffset.UnixEpoch));
    using var failure = factory.CreateRfidWriteEvent(new RfidWriteEvent(
        "SHELF001",
        "LOC001",
        "ABC",
        7,
        "RFID Verify Mismatch",
        DateTimeOffset.UnixEpoch));

    Equal((uint)2002, success.SecsItem![1].FirstValue<uint>(), "RFID success CEID");
    Equal((uint)2005, failure.SecsItem![1].FirstValue<uint>(), "RFID failure CEID");
    Equal((byte)7, failure.SecsItem[2][0][1][3].FirstValue<byte>(), "RFID failure report code");
    return Task.CompletedTask;
}

static Task TestShelfStateEvent()
{
    var factory = new SecsEventMessageFactory(Options.Create(new SecsEventOptions()));
    using var message = factory.CreateShelfStateEvent(new ShelfStateEvent(
        "SHELF001",
        "LOC001",
        "ABC",
        true,
        DateTimeOffset.UnixEpoch));

    Equal((byte)6, message.S, "S6F21 stream");
    Equal((byte)21, message.F, "S6F21 function");
    Equal(5, message.SecsItem!.Count, "S6F21 root count");
    return Task.CompletedTask;
}

static async Task TestSensorWriteSuccessAsync()
{
    var hardware = new FakeHardwareGateway
    {
        QueryResult = ShelfStatusResult.Ok(
            "SHELF001",
            new[] { new ShelfLocationStatus("LOC001", "ABC   OLD", true) })
    };
    var workflow = CreateWorkflow(hardware, enabled: true, presenceMode: "Sensor");
    var result = await workflow.ExecuteAsync(
        new TagWriteCommand("SHELF001", "LOC001", "ABC   "),
        CancellationToken.None);

    True(result.Success, "sensor write success result");
    Equal(1, hardware.QueryCount, "sensor write query count");
    Equal(6, hardware.LastQuery?.ReadLengthBytes, "sensor write query length");
    Equal("ABC", hardware.LastDisplay?.Content, "RFID display boundary trimming");
}

static async Task TestSensorWriteMismatchAsync()
{
    var hardware = new FakeHardwareGateway
    {
        QueryResult = ShelfStatusResult.Ok(
            "SHELF001",
            new[] { new ShelfLocationStatus("LOC001", "ABD   OLD", true) })
    };
    var workflow = CreateWorkflow(hardware, enabled: true, presenceMode: "Sensor");
    var result = await workflow.ExecuteAsync(
        new TagWriteCommand("SHELF001", "LOC001", "ABC   "),
        CancellationToken.None);

    Equal((byte)7, result.Code, "mismatch result code");
    Equal("RFID Verify Mismatch", result.Description, "mismatch description");
    Equal(0, hardware.DisplayCount, "mismatch display count");
}

static async Task TestSensorWriteReadFailureAsync()
{
    var hardware = new FakeHardwareGateway
    {
        QueryResult = ShelfStatusResult.Fail("SHELF001", 7, "timeout")
    };
    var workflow = CreateWorkflow(hardware, enabled: true, presenceMode: "Sensor");
    var result = await workflow.ExecuteAsync(
        new TagWriteCommand("SHELF001", "LOC001", "ABC"),
        CancellationToken.None);

    Equal((byte)7, result.Code, "read failure result code");
    Equal("RFID Verify Read Failed", result.Description, "read failure description");
    Equal(0, hardware.DisplayCount, "read failure display count");
}

static async Task TestSensorDisplayFailureAsync()
{
    var hardware = new FakeHardwareGateway
    {
        QueryResult = ShelfStatusResult.Ok(
            "SHELF001",
            new[] { new ShelfLocationStatus("LOC001", "ABC", true) }),
        DisplayResult = OperationResult.Fail(8, "display offline")
    };
    var workflow = CreateWorkflow(hardware, enabled: true, presenceMode: "Sensor");
    var result = await workflow.ExecuteAsync(
        new TagWriteCommand("SHELF001", "LOC001", "ABC"),
        CancellationToken.None);

    True(result.Success, "display failure must keep write success");
    Equal(1, hardware.DisplayCount, "display failure call count");
}

static async Task TestRfidPollingSkipsVerifyAsync()
{
    var hardware = new FakeHardwareGateway();
    var workflow = CreateWorkflow(hardware, enabled: true, presenceMode: "RfidPolling");
    var result = await workflow.ExecuteAsync(
        new TagWriteCommand("SHELF001", "LOC001", "ABC"),
        CancellationToken.None);

    True(result.Success, "RFID polling write result");
    Equal(0, hardware.QueryCount, "RFID polling immediate query count");
    Equal(0, hardware.DisplayCount, "RFID polling immediate display count");
}

static SecsMessage DisplayRequest(Item content)
{
    return new SecsMessage(10, 3)
    {
        SecsItem = L(A("SHELF001"), A("LOC001"), content)
    };
}

static void AssertDisplayReply(SecsMessage? reply, byte code, string description)
{
    Equal((byte)10, reply!.S, "S10F4 stream");
    Equal((byte)4, reply.F, "S10F4 function");
    Equal(4, reply.SecsItem!.Count, "S10F4 root count");
    Equal(code, reply.SecsItem[2].FirstValue<byte>(), "S10F4 result code");
    Equal(description, reply.SecsItem[3].GetString(), "S10F4 description");
}

static RfidWriteWorkflow CreateWorkflow(
    FakeHardwareGateway hardware,
    bool enabled,
    string presenceMode)
{
    var options = new ERackSensorDisplayOptions
    {
        Enabled = enabled,
        PresenceMode = presenceMode,
        DisplayMaxBytes = 512
    };
    var displayCommands = new DisplayCommandService(hardware, Options.Create(options));
    return new RfidWriteWorkflow(
        hardware,
        displayCommands,
        Options.Create(options),
        new StatusUiEventBus(),
        NullLogger<RfidWriteWorkflow>.Instance);
}

static DispatcherFixture CreateFixture(int displayMaxBytes = 512, string runtimeMode = "Unit")
{
    var hardware = new FakeHardwareGateway();
    var sensorOptions = new ERackSensorDisplayOptions
    {
        Enabled = false,
        PresenceMode = "Sensor",
        DisplayMaxBytes = displayMaxBytes
    };
    var statusEvents = new StatusUiEventBus();
    var displayCommands = new DisplayCommandService(hardware, Options.Create(sensorOptions));
    var writeWorkflow = new RfidWriteWorkflow(
        hardware,
        displayCommands,
        Options.Create(sensorOptions),
        statusEvents,
        NullLogger<RfidWriteWorkflow>.Instance);
    var registry = new ERackLocationRegistry(
        new ConfigurationBuilder().Build(),
        Options.Create(new ERackHardwareOptions
        {
            DefaultShelfId = "SHELF001",
            DefaultLocationId = "LOC001"
        }));
    var router = new FakeUnitRouter();
    var dispatcher = new SecsMessageDispatcher(
        new FakePrinterGateway(),
        hardware,
        writeWorkflow,
        displayCommands,
        Options.Create(new RuntimeOptions { Mode = runtimeMode }),
        Options.Create(sensorOptions),
        new RfidPollingStateCache(registry),
        router,
        new FakeEventSink(),
        statusEvents,
        NullLogger<SecsMessageDispatcher>.Instance);
    return new DispatcherFixture(dispatcher, hardware, router);
}

static void True(bool value, string name)
{
    if (!value)
    {
        throw new InvalidOperationException($"Assertion failed: {name}");
    }
}

static void Equal<T>(T expected, T actual, string name)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException(
            $"Assertion failed: {name}; expected={expected}, actual={actual}");
    }
}

internal sealed record DispatcherFixture(
    SecsMessageDispatcher Dispatcher,
    FakeHardwareGateway Hardware,
    FakeUnitRouter Router);

internal sealed class FakeHardwareGateway : IHardwareGateway
{
    public OperationResult WriteResult { get; set; } = OperationResult.Ok("tag written");
    public ShelfStatusResult QueryResult { get; set; } = ShelfStatusResult.Ok(
        "SHELF001",
        new[] { new ShelfLocationStatus("LOC001", "ABC", true) });
    public OperationResult DisplayResult { get; set; } = OperationResult.Ok("display updated");
    public int QueryCount { get; private set; }
    public int DisplayCount { get; private set; }
    public ShelfStatusQuery? LastQuery { get; private set; }
    public DisplayCommand? LastDisplay { get; private set; }

    public Task<OperationResult> WriteTagAsync(TagWriteCommand command, CancellationToken cancellationToken)
    {
        return Task.FromResult(WriteResult);
    }

    public Task<ShelfStatusResult> QueryShelfStatusAsync(
        ShelfStatusQuery query,
        CancellationToken cancellationToken)
    {
        QueryCount++;
        LastQuery = query;
        return Task.FromResult(QueryResult);
    }

    public Task<OperationResult> SetDisplayAsync(
        DisplayCommand command,
        CancellationToken cancellationToken)
    {
        DisplayCount++;
        LastDisplay = command;
        return Task.FromResult(DisplayResult);
    }
}

internal sealed class FakeUnitRouter : IERackUnitRouter
{
    public OperationResult DisplayResult { get; set; } = OperationResult.Ok("Set Success");
    public DisplayCommand? LastDisplay { get; private set; }

    public Task<ShelfStatusResult> QueryShelfStatusAsync(
        ShelfStatusQuery query,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(ShelfStatusResult.Fail(query.ShelfId, 7, "not configured"));
    }

    public Task<OperationResult> WriteTagAsync(
        TagWriteCommand command,
        CancellationToken cancellationToken)
    {
        return Task.FromResult(OperationResult.Ok());
    }

    public Task<OperationResult> SetDisplayAsync(
        DisplayCommand command,
        CancellationToken cancellationToken)
    {
        LastDisplay = command;
        return Task.FromResult(DisplayResult);
    }

    public Task<OperationResult> PrintAsync(PrintCommand command, CancellationToken cancellationToken)
    {
        return Task.FromResult(OperationResult.Ok());
    }
}

internal sealed class FakePrinterGateway : IPrinterGateway
{
    public Task<OperationResult> PrintAsync(PrintCommand command, CancellationToken cancellationToken)
    {
        return Task.FromResult(OperationResult.Ok());
    }
}

internal sealed class FakeEventSink : IERackEventSink
{
    public Task PublishShelfStateAsync(ShelfStateEvent shelfStateEvent, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task PublishRfidWriteAsync(RfidWriteEvent rfidWriteEvent, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task PublishPrintAsync(PrintEvent printEvent, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }
}
