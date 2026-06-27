using PrinterSecsGem.Eq.Models;

namespace PrinterSecsGem.Eq.ErackNetwork;

public interface IERackUnitRouter
{
    Task<ShelfStatusResult> QueryShelfStatusAsync(ShelfStatusQuery query, CancellationToken cancellationToken);

    Task<OperationResult> WriteTagAsync(TagWriteCommand command, CancellationToken cancellationToken);

    Task<OperationResult> PrintAsync(PrintCommand command, CancellationToken cancellationToken);
}
