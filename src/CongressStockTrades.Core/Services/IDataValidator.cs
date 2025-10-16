using CongressStockTrades.Core.Models;

namespace CongressStockTrades.Core.Services;

public interface IDataValidator
{
    /// <summary>
    /// Validates extracted transaction data against expected values
    /// </summary>
    /// <param name="document">The transaction document to validate</param>
    /// <param name="expectedName">Expected politician name from website</param>
    /// <param name="expectedOffice">Expected office/district from website</param>
    /// <exception cref="System.Exception">Thrown when validation fails</exception>
    void Validate(TransactionDocument document, string expectedName, string expectedOffice);
}
