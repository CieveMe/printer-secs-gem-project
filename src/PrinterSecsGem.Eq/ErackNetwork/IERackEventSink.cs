using PrinterSecsGem.Eq.Models;

namespace PrinterSecsGem.Eq.ErackNetwork;

public interface IERackEventSink
{
    Task PublishShelfStateAsync(ShelfStateEvent shelfStateEvent, CancellationToken cancellationToken);

    Task PublishRfidWriteAsync(RfidWriteEvent rfidWriteEvent, CancellationToken cancellationToken);

    Task PublishPrintAsync(PrintEvent printEvent, CancellationToken cancellationToken);
}
