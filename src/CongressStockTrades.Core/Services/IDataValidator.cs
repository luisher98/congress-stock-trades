using CongressStockTrades.Core.Models;

namespace CongressStockTrades.Core.Services;

public interface IDataValidator
{
    /// <summary>
    /// Validates extracted transaction data against expected values
    /// </summary>
    /// <exception cref="ValidationException">Thrown when validation fails</exception>
    void Validate(TransactionDocument document, string expectedName, string expectedOffice);
}
