using PrinterSecsGem.Eq.Models;

namespace PrinterSecsGem.Eq.Hardware;

public interface IHardwareGateway
{
    Task<OperationResult> WriteTagAsync(TagWriteCommand command, CancellationToken cancellationToken);

    Task<ShelfStatusResult> QueryShelfStatusAsync(ShelfStatusQuery query, CancellationToken cancellationToken);
}
