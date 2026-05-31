using PrinterSecsGem.Eq.Models;

namespace PrinterSecsGem.Eq.Printing;

public interface IPrinterGateway
{
    Task<OperationResult> PrintAsync(PrintCommand command, CancellationToken cancellationToken);
}
