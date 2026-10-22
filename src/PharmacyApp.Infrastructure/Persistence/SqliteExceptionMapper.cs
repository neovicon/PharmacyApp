using Microsoft.Data.Sqlite;
using PharmacyApp.Application.Exceptions;

namespace PharmacyApp.Infrastructure.Persistence;

/// <summary>
/// Maps low-level SQLite constraint error codes to meaningful domain/application exceptions
/// without masking unexpected engine or filesystem failures.
/// </summary>
internal static class SqliteExceptionMapper
{
    public static Exception Map(SqliteException ex, string? entityName = null, string? contextValue = null)
    {
        // SQLite Error 19 corresponds to SQLITE_CONSTRAINT
        if (ex.SqliteErrorCode == 19 || ex.Message.Contains("constraint failed", StringComparison.OrdinalIgnoreCase))
        {
            if (ex.Message.Contains("Medicines.Barcode", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("UX_Medicines_Barcode", StringComparison.OrdinalIgnoreCase))
            {
                return new DuplicateBarcodeException(
                    contextValue != null
                        ? $"A medicine with barcode '{contextValue}' already exists."
                        : ex.Message,
                    ex,
                    contextValue);
            }

            if (ex.Message.Contains("Batches.BatchCode", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("IX_Batches_BatchCode", StringComparison.OrdinalIgnoreCase))
            {
                return new DuplicateBatchCodeException(
                    contextValue != null
                        ? $"A batch with batch code '{contextValue}' already exists."
                        : ex.Message,
                    ex,
                    contextValue);
            }

            if (ex.Message.Contains("StockAdjustments ledger entries are immutable", StringComparison.OrdinalIgnoreCase) ||
                ex.Message.Contains("TR_StockAdjustments", StringComparison.OrdinalIgnoreCase))
            {
                return new ImmutableLedgerException("Stock adjustments are immutable audit ledger entries and cannot be modified or deleted.", ex);
            }

            if (ex.SqliteExtendedErrorCode == 787 ||
                ex.Message.Contains("FOREIGN KEY constraint failed", StringComparison.OrdinalIgnoreCase))
            {
                return new ForeignKeyConstraintException($"Foreign key constraint violation in {entityName ?? "entity"}: {ex.Message}", ex);
            }

            if (ex.SqliteExtendedErrorCode == 275 ||
                ex.Message.Contains("CHECK constraint failed", StringComparison.OrdinalIgnoreCase))
            {
                return new DataConstraintException($"Check constraint violation in {entityName ?? "entity"}: {ex.Message}", ex);
            }

            // General unique constraint violation fallback
            if (ex.SqliteExtendedErrorCode == 2067 ||
                ex.Message.Contains("UNIQUE constraint failed", StringComparison.OrdinalIgnoreCase))
            {
                return new InvalidOperationException($"Unique constraint violated in {entityName ?? "entity"}: {ex.Message}", ex);
            }
        }

        return ex;
    }
}
