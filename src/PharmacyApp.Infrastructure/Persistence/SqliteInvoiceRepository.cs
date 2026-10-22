using System.Data;
using Microsoft.Data.Sqlite;
using PharmacyApp.Application.Abstractions;
using PharmacyApp.Domain.Entities;
using PharmacyApp.Domain.Enums;
using PharmacyApp.Domain.ValueObjects;

namespace PharmacyApp.Infrastructure.Persistence;

public sealed class SqliteInvoiceRepository : IInvoiceRepository
{
    private readonly string _connectionString;

    public SqliteInvoiceRepository(string connectionString)
    {
        _connectionString = connectionString;
    }

    public async Task<Guid> AddAsync(Invoice invoice, CancellationToken cancellationToken = default, object? transaction = null)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        SqliteTransaction? tx = transaction as SqliteTransaction;
        SqliteConnection? conn = tx?.Connection;
        bool shouldDisposeConn = false;

        if (conn is null)
        {
            conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            shouldDisposeConn = true;
        }

        try
        {
            await using (var cmd = conn.CreateCommand())
            {
                if (tx is not null) cmd.Transaction = tx;

                cmd.CommandText = """
                    INSERT INTO Invoices (
                        Id, InvoiceNumber, CustomerName, PrescriptionNumber, PaymentMethod,
                        SubTotalPaisa, DiscountPaisa, TaxPaisa, TotalAmountPaisa, UserId,
                        Notes, Status, CreatedAt, UpdatedAt, Version
                    ) VALUES (
                        $id, $invoiceNumber, $customerName, $prescriptionNumber, $paymentMethod,
                        $subTotalPaisa, $discountPaisa, $taxPaisa, $totalAmountPaisa, $userId,
                        $notes, $status, $createdAt, $updatedAt, $version
                    );
                    """;

                cmd.Parameters.AddWithValue("$id", invoice.Id.ToString());
                cmd.Parameters.AddWithValue("$invoiceNumber", invoice.InvoiceNumber);
                cmd.Parameters.AddWithValue("$customerName", (object?)invoice.CustomerName ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$prescriptionNumber", (object?)invoice.PrescriptionNumber ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$paymentMethod", (int)invoice.PaymentMethod);
                cmd.Parameters.AddWithValue("$subTotalPaisa", invoice.SubTotal.ToPaisa());
                cmd.Parameters.AddWithValue("$discountPaisa", invoice.Discount.ToPaisa());
                cmd.Parameters.AddWithValue("$taxPaisa", invoice.Tax.ToPaisa());
                cmd.Parameters.AddWithValue("$totalAmountPaisa", invoice.TotalAmount.ToPaisa());
                cmd.Parameters.AddWithValue("$userId", invoice.UserId.ToString());
                cmd.Parameters.AddWithValue("$notes", (object?)invoice.Notes ?? DBNull.Value);
                cmd.Parameters.AddWithValue("$status", (int)invoice.Status);
                cmd.Parameters.AddWithValue("$createdAt", invoice.CreatedAt.ToString("O"));
                cmd.Parameters.AddWithValue("$updatedAt", invoice.UpdatedAt.ToString("O"));
                cmd.Parameters.AddWithValue("$version", invoice.Version);

                await cmd.ExecuteNonQueryAsync(cancellationToken);
            }

            foreach (var item in invoice.Items)
            {
                await using var itemCmd = conn.CreateCommand();
                if (tx is not null) itemCmd.Transaction = tx;

                itemCmd.CommandText = """
                    INSERT INTO InvoiceItems (
                        Id, InvoiceId, BatchId, MedicineId, MedicineName,
                        BatchCode, Quantity, UnitPricePaisa, LineTotalPaisa
                    ) VALUES (
                        $id, $invoiceId, $batchId, $medicineId, $medicineName,
                        $batchCode, $quantity, $unitPricePaisa, $lineTotalPaisa
                    );
                    """;

                itemCmd.Parameters.AddWithValue("$id", item.Id.ToString());
                itemCmd.Parameters.AddWithValue("$invoiceId", invoice.Id.ToString());
                itemCmd.Parameters.AddWithValue("$batchId", item.BatchId.ToString());
                itemCmd.Parameters.AddWithValue("$medicineId", item.MedicineId.ToString());
                itemCmd.Parameters.AddWithValue("$medicineName", item.MedicineName);
                itemCmd.Parameters.AddWithValue("$batchCode", item.BatchCode);
                itemCmd.Parameters.AddWithValue("$quantity", item.Quantity);
                itemCmd.Parameters.AddWithValue("$unitPricePaisa", item.UnitPrice.ToPaisa());
                itemCmd.Parameters.AddWithValue("$lineTotalPaisa", item.LineTotal.ToPaisa());

                await itemCmd.ExecuteNonQueryAsync(cancellationToken);
            }

            return invoice.Id;
        }
        finally
        {
            if (shouldDisposeConn)
            {
                await conn.DisposeAsync();
            }
        }
    }

    public async Task<Invoice?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default, object? transaction = null)
    {
        SqliteTransaction? tx = transaction as SqliteTransaction;
        SqliteConnection? conn = tx?.Connection;
        bool shouldDisposeConn = false;

        if (conn is null)
        {
            conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            shouldDisposeConn = true;
        }

        try
        {
            Invoice? invoice = null;

            await using (var cmd = conn.CreateCommand())
            {
                if (tx is not null) cmd.Transaction = tx;

                cmd.CommandText = """
                    SELECT Id, InvoiceNumber, CustomerName, PrescriptionNumber, PaymentMethod,
                           SubTotalPaisa, DiscountPaisa, TaxPaisa, TotalAmountPaisa, UserId,
                           Notes, Status, CreatedAt, UpdatedAt, Version
                    FROM Invoices
                    WHERE Id = $id;
                    """;
                cmd.Parameters.AddWithValue("$id", id.ToString());

                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    invoice = MapInvoice(reader);
                }
            }

            if (invoice is not null)
            {
                var items = await LoadInvoiceItemsAsync(conn, tx, invoice.Id, cancellationToken);
                invoice.AddItems(items);
            }

            return invoice;
        }
        finally
        {
            if (shouldDisposeConn)
            {
                await conn.DisposeAsync();
            }
        }
    }

    public async Task<Invoice?> GetByInvoiceNumberAsync(string invoiceNumber, CancellationToken cancellationToken = default, object? transaction = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(invoiceNumber);

        SqliteTransaction? tx = transaction as SqliteTransaction;
        SqliteConnection? conn = tx?.Connection;
        bool shouldDisposeConn = false;

        if (conn is null)
        {
            conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            shouldDisposeConn = true;
        }

        try
        {
            Invoice? invoice = null;

            await using (var cmd = conn.CreateCommand())
            {
                if (tx is not null) cmd.Transaction = tx;

                cmd.CommandText = """
                    SELECT Id, InvoiceNumber, CustomerName, PrescriptionNumber, PaymentMethod,
                           SubTotalPaisa, DiscountPaisa, TaxPaisa, TotalAmountPaisa, UserId,
                           Notes, Status, CreatedAt, UpdatedAt, Version
                    FROM Invoices
                    WHERE InvoiceNumber = $invoiceNumber;
                    """;
                cmd.Parameters.AddWithValue("$invoiceNumber", invoiceNumber.Trim());

                await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
                if (await reader.ReadAsync(cancellationToken))
                {
                    invoice = MapInvoice(reader);
                }
            }

            if (invoice is not null)
            {
                var items = await LoadInvoiceItemsAsync(conn, tx, invoice.Id, cancellationToken);
                invoice.AddItems(items);
            }

            return invoice;
        }
        finally
        {
            if (shouldDisposeConn)
            {
                await conn.DisposeAsync();
            }
        }
    }

    public async Task<IReadOnlyList<Invoice>> ListRecentAsync(int limit = 50, string? searchTerm = null, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        var invoices = new List<Invoice>();

        await using (var cmd = conn.CreateCommand())
        {
            string sql = """
                SELECT Id, InvoiceNumber, CustomerName, PrescriptionNumber, PaymentMethod,
                       SubTotalPaisa, DiscountPaisa, TaxPaisa, TotalAmountPaisa, UserId,
                       Notes, Status, CreatedAt, UpdatedAt, Version
                FROM Invoices
                """;

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                sql += " WHERE (InvoiceNumber LIKE $term OR CustomerName LIKE $term OR PrescriptionNumber LIKE $term) ";
                cmd.Parameters.AddWithValue("$term", $"%{searchTerm.Trim()}%");
            }

            sql += " ORDER BY CreatedAt DESC LIMIT $limit;";
            cmd.Parameters.AddWithValue("$limit", Math.Max(1, limit));
            cmd.CommandText = sql;

            await using var reader = await cmd.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                invoices.Add(MapInvoice(reader));
            }
        }

        foreach (var inv in invoices)
        {
            var items = await LoadInvoiceItemsAsync(conn, null, inv.Id, cancellationToken);
            inv.AddItems(items);
        }

        return invoices;
    }

    public async Task<int> GetCountAsync(CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT COUNT(*) FROM Invoices;";
        var count = await cmd.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(count);
    }

    public async Task<decimal> GetTotalSalesAmountAsync(DateTimeOffset? from = null, DateTimeOffset? to = null, CancellationToken cancellationToken = default)
    {
        await using var conn = new SqliteConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        await using var cmd = conn.CreateCommand();
        string sql = "SELECT COALESCE(SUM(TotalAmountPaisa), 0) FROM Invoices WHERE Status = 1";

        if (from.HasValue)
        {
            sql += " AND CreatedAt >= $from";
            cmd.Parameters.AddWithValue("$from", from.Value.ToString("O"));
        }

        if (to.HasValue)
        {
            sql += " AND CreatedAt <= $to";
            cmd.Parameters.AddWithValue("$to", to.Value.ToString("O"));
        }

        cmd.CommandText = sql;
        var totalPaisa = await cmd.ExecuteScalarAsync(cancellationToken);
        long paisa = Convert.ToInt64(totalPaisa);
        return paisa / 100m;
    }

    public async Task UpdateAsync(Invoice invoice, uint expectedVersion, CancellationToken cancellationToken = default, object? transaction = null)
    {
        ArgumentNullException.ThrowIfNull(invoice);

        SqliteTransaction? tx = transaction as SqliteTransaction;
        SqliteConnection? conn = tx?.Connection;
        bool shouldDisposeConn = false;

        if (conn is null)
        {
            conn = new SqliteConnection(_connectionString);
            await conn.OpenAsync(cancellationToken);
            shouldDisposeConn = true;
        }

        try
        {
            await using var cmd = conn.CreateCommand();
            if (tx is not null) cmd.Transaction = tx;

            cmd.CommandText = """
                UPDATE Invoices
                SET Notes = $notes,
                    Status = $status,
                    UpdatedAt = $updatedAt,
                    Version = Version + 1
                WHERE Id = $id AND Version = $expectedVersion;
                """;

            cmd.Parameters.AddWithValue("$notes", (object?)invoice.Notes ?? DBNull.Value);
            cmd.Parameters.AddWithValue("$status", (int)invoice.Status);
            cmd.Parameters.AddWithValue("$updatedAt", invoice.UpdatedAt.ToString("O"));
            cmd.Parameters.AddWithValue("$id", invoice.Id.ToString());
            cmd.Parameters.AddWithValue("$expectedVersion", expectedVersion);

            int rows = await cmd.ExecuteNonQueryAsync(cancellationToken);
            if (rows == 0)
            {
                throw new DBConcurrencyException($"Invoice '{invoice.InvoiceNumber}' optimistic concurrency check failed. Expected version: {expectedVersion}.");
            }
        }
        finally
        {
            if (shouldDisposeConn)
            {
                await conn.DisposeAsync();
            }
        }
    }

    private static async Task<List<InvoiceItem>> LoadInvoiceItemsAsync(SqliteConnection conn, SqliteTransaction? tx, Guid invoiceId, CancellationToken ct)
    {
        var items = new List<InvoiceItem>();

        await using var cmd = conn.CreateCommand();
        if (tx is not null) cmd.Transaction = tx;

        cmd.CommandText = """
            SELECT Id, InvoiceId, BatchId, MedicineId, MedicineName, BatchCode,
                   Quantity, UnitPricePaisa, LineTotalPaisa
            FROM InvoiceItems
            WHERE InvoiceId = $invoiceId;
            """;
        cmd.Parameters.AddWithValue("$invoiceId", invoiceId.ToString());

        await using var reader = await cmd.ExecuteReaderAsync(ct);
        while (await reader.ReadAsync(ct))
        {
            items.Add(InvoiceItem.Load(
                id: Guid.Parse(reader.GetString(0)),
                invoiceId: Guid.Parse(reader.GetString(1)),
                batchId: Guid.Parse(reader.GetString(2)),
                medicineId: Guid.Parse(reader.GetString(3)),
                medicineName: reader.GetString(4),
                batchCode: reader.GetString(5),
                quantity: reader.GetInt32(6),
                unitPrice: Money.FromPaisa(reader.GetInt64(7)),
                lineTotal: Money.FromPaisa(reader.GetInt64(8))));
        }

        return items;
    }

    private static Invoice MapInvoice(SqliteDataReader reader)
    {
        return Invoice.Load(
            id: Guid.Parse(reader.GetString(0)),
            invoiceNumber: reader.GetString(1),
            customerName: reader.IsDBNull(2) ? null : reader.GetString(2),
            prescriptionNumber: reader.IsDBNull(3) ? null : reader.GetString(3),
            paymentMethod: (PaymentMethod)reader.GetInt32(4),
            subTotal: Money.FromPaisa(reader.GetInt64(5)),
            discount: Money.FromPaisa(reader.GetInt64(6)),
            tax: Money.FromPaisa(reader.GetInt64(7)),
            totalAmount: Money.FromPaisa(reader.GetInt64(8)),
            userId: Guid.Parse(reader.GetString(9)),
            notes: reader.IsDBNull(10) ? null : reader.GetString(10),
            status: (InvoiceStatus)reader.GetInt32(11),
            createdAt: DateTimeOffset.Parse(reader.GetString(12)),
            updatedAt: DateTimeOffset.Parse(reader.GetString(13)),
            version: Convert.ToUInt32(reader.GetInt64(14)));
    }
}
