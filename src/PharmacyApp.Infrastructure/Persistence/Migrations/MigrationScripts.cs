using System.Collections.Generic;

namespace PharmacyApp.Infrastructure.Persistence.Migrations;

/// <summary>
/// Represents a versioned schema migration.
/// </summary>
public sealed record MigrationDefinition(
    int Version,
    string Description,
    string UpSql,
    IReadOnlyList<string> TargetObjects);

/// <summary>
/// Provides all versioned SQLite DDL scripts for schema migrations.
/// Scripts are fail-closed: NO 'IF NOT EXISTS' clauses are used.
/// </summary>
public static class MigrationScripts
{
    public const string InitialAuthSql = """
        CREATE TABLE Users (
            Id TEXT PRIMARY KEY,
            Username TEXT NOT NULL UNIQUE COLLATE NOCASE,
            DisplayName TEXT NOT NULL,
            PasswordHash TEXT NOT NULL,
            Role INTEGER NOT NULL,
            IsActive INTEGER NOT NULL DEFAULT 1,
            CreatedAt TEXT NOT NULL,
            UpdatedAt TEXT NOT NULL,
            Version INTEGER NOT NULL DEFAULT 1
        );
        CREATE INDEX IX_Users_Username ON Users(Username);
        """;

    public const string MedicineCatalogueAndBatchesSql = """
        CREATE TABLE Medicines (
            Id TEXT PRIMARY KEY,
            Name TEXT NOT NULL COLLATE NOCASE,
            GenericName TEXT NOT NULL COLLATE NOCASE,
            Brand TEXT NULL,
            Strength TEXT NULL,
            DosageForm TEXT NOT NULL,
            DispensingUnit TEXT NOT NULL,
            Barcode TEXT NULL,
            Category TEXT NULL,
            ReorderLevel INTEGER NOT NULL,
            RequiresPrescription INTEGER NOT NULL DEFAULT 0,
            IsActive INTEGER NOT NULL DEFAULT 1,
            CreatedAt TEXT NOT NULL,
            UpdatedAt TEXT NOT NULL,
            Version INTEGER NOT NULL DEFAULT 1,
            CONSTRAINT CK_Medicines_ReorderLevel CHECK (ReorderLevel >= 0),
            CONSTRAINT CK_Medicines_RequiresPrescription CHECK (RequiresPrescription IN (0, 1)),
            CONSTRAINT CK_Medicines_IsActive CHECK (IsActive IN (0, 1))
        );

        CREATE INDEX IX_Medicines_Name ON Medicines(Name);
        CREATE INDEX IX_Medicines_GenericName ON Medicines(GenericName);
        CREATE INDEX IX_Medicines_Category ON Medicines(Category);
        CREATE UNIQUE INDEX UX_Medicines_Barcode ON Medicines(Barcode) WHERE Barcode IS NOT NULL;

        CREATE TABLE Batches (
            Id TEXT PRIMARY KEY,
            MedicineId TEXT NOT NULL,
            BatchCode TEXT NOT NULL UNIQUE,
            ManufacturerBatchNumber TEXT NULL,
            ExpiryDate TEXT NULL,
            QuantityOnHand INTEGER NOT NULL,
            PurchaseCostPaisa INTEGER NOT NULL,
            SellingPricePaisa INTEGER NOT NULL,
            Location TEXT NULL,
            IsActive INTEGER NOT NULL DEFAULT 1,
            CreatedAt TEXT NOT NULL,
            UpdatedAt TEXT NOT NULL,
            Version INTEGER NOT NULL DEFAULT 1,
            CONSTRAINT FK_Batches_Medicines FOREIGN KEY (MedicineId)
                REFERENCES Medicines(Id) ON DELETE RESTRICT,
            CONSTRAINT CK_Batches_QuantityOnHand CHECK (QuantityOnHand >= 0),
            CONSTRAINT CK_Batches_PurchaseCostPaisa CHECK (PurchaseCostPaisa >= 0),
            CONSTRAINT CK_Batches_SellingPricePaisa CHECK (SellingPricePaisa >= 0),
            CONSTRAINT CK_Batches_IsActive CHECK (IsActive IN (0, 1))
        );

        CREATE INDEX IX_Batches_MedicineId ON Batches(MedicineId);
        CREATE INDEX IX_Batches_BatchCode ON Batches(BatchCode);
        CREATE INDEX IX_Batches_ExpiryDate ON Batches(ExpiryDate);
        CREATE INDEX IX_Batches_IsActive ON Batches(IsActive);
        CREATE UNIQUE INDEX UX_Batches_Id_MedicineId ON Batches(Id, MedicineId);

        CREATE TABLE StockAdjustments (
            Id TEXT PRIMARY KEY,
            BatchId TEXT NOT NULL,
            MedicineId TEXT NOT NULL,
            AdjustmentType INTEGER NOT NULL,
            PreviousQuantity INTEGER NOT NULL,
            QuantityDelta INTEGER NOT NULL,
            NewQuantity INTEGER NOT NULL,
            Reason TEXT NOT NULL,
            UserId TEXT NOT NULL,
            Timestamp TEXT NOT NULL,
            CONSTRAINT FK_StockAdjustments_Batches FOREIGN KEY (BatchId, MedicineId)
                REFERENCES Batches(Id, MedicineId) ON DELETE RESTRICT,
            CONSTRAINT FK_StockAdjustments_Users FOREIGN KEY (UserId)
                REFERENCES Users(Id) ON DELETE RESTRICT,
            CONSTRAINT CK_StockAdjustments_PreviousQuantity CHECK (PreviousQuantity >= 0),
            CONSTRAINT CK_StockAdjustments_NewQuantity CHECK (NewQuantity >= 0),
            CONSTRAINT CK_StockAdjustments_Arithmetic CHECK (PreviousQuantity + QuantityDelta = NewQuantity)
        );

        CREATE INDEX IX_StockAdjustments_BatchId ON StockAdjustments(BatchId);
        CREATE INDEX IX_StockAdjustments_MedicineId ON StockAdjustments(MedicineId);
        CREATE INDEX IX_StockAdjustments_UserId ON StockAdjustments(UserId);
        CREATE INDEX IX_StockAdjustments_Timestamp ON StockAdjustments(Timestamp);

        CREATE TRIGGER TR_StockAdjustments_PreventUpdate
        BEFORE UPDATE ON StockAdjustments
        BEGIN
            SELECT RAISE(FAIL, 'StockAdjustments ledger entries are immutable and cannot be updated.');
        END;

        CREATE TRIGGER TR_StockAdjustments_PreventDelete
        BEFORE DELETE ON StockAdjustments
        BEGIN
            SELECT RAISE(FAIL, 'StockAdjustments ledger entries are immutable and cannot be deleted.');
        END;
        """;

    public const string SuppliersSql = """
        CREATE TABLE Suppliers (
            Id TEXT PRIMARY KEY,
            Name TEXT NOT NULL COLLATE NOCASE,
            ContactPerson TEXT NULL,
            Phone TEXT NULL,
            Email TEXT NULL,
            PanNumber TEXT NULL,
            Address TEXT NULL,
            IsActive INTEGER NOT NULL DEFAULT 1,
            CreatedAt TEXT NOT NULL,
            UpdatedAt TEXT NOT NULL,
            Version INTEGER NOT NULL DEFAULT 1,
            CONSTRAINT CK_Suppliers_IsActive CHECK (IsActive IN (0, 1))
        );

        CREATE INDEX IX_Suppliers_Name ON Suppliers(Name);
        CREATE INDEX IX_Suppliers_PanNumber ON Suppliers(PanNumber);
        CREATE INDEX IX_Suppliers_IsActive ON Suppliers(IsActive);
        """;

    public const string InvoicesAndSalesHistorySql = """
        CREATE TABLE Invoices (
            Id TEXT PRIMARY KEY,
            InvoiceNumber TEXT NOT NULL UNIQUE,
            CustomerName TEXT NULL,
            PrescriptionNumber TEXT NULL,
            PaymentMethod INTEGER NOT NULL,
            SubTotalPaisa INTEGER NOT NULL,
            DiscountPaisa INTEGER NOT NULL DEFAULT 0,
            TaxPaisa INTEGER NOT NULL DEFAULT 0,
            TotalAmountPaisa INTEGER NOT NULL,
            UserId TEXT NOT NULL,
            Notes TEXT NULL,
            Status INTEGER NOT NULL DEFAULT 1,
            CreatedAt TEXT NOT NULL,
            UpdatedAt TEXT NOT NULL,
            Version INTEGER NOT NULL DEFAULT 1,
            CONSTRAINT CK_Invoices_SubTotalPaisa CHECK (SubTotalPaisa >= 0),
            CONSTRAINT CK_Invoices_DiscountPaisa CHECK (DiscountPaisa >= 0),
            CONSTRAINT CK_Invoices_TaxPaisa CHECK (TaxPaisa >= 0),
            CONSTRAINT CK_Invoices_TotalAmountPaisa CHECK (TotalAmountPaisa >= 0)
        );

        CREATE INDEX IX_Invoices_InvoiceNumber ON Invoices(InvoiceNumber);
        CREATE INDEX IX_Invoices_CreatedAt ON Invoices(CreatedAt);
        CREATE INDEX IX_Invoices_UserId ON Invoices(UserId);

        CREATE TABLE InvoiceItems (
            Id TEXT PRIMARY KEY,
            InvoiceId TEXT NOT NULL,
            BatchId TEXT NOT NULL,
            MedicineId TEXT NOT NULL,
            MedicineName TEXT NOT NULL,
            BatchCode TEXT NOT NULL,
            Quantity INTEGER NOT NULL,
            UnitPricePaisa INTEGER NOT NULL,
            LineTotalPaisa INTEGER NOT NULL,
            CONSTRAINT FK_InvoiceItems_Invoices FOREIGN KEY (InvoiceId)
                REFERENCES Invoices(Id) ON DELETE CASCADE,
            CONSTRAINT CK_InvoiceItems_Quantity CHECK (Quantity > 0),
            CONSTRAINT CK_InvoiceItems_UnitPricePaisa CHECK (UnitPricePaisa >= 0),
            CONSTRAINT CK_InvoiceItems_LineTotalPaisa CHECK (LineTotalPaisa >= 0)
        );

        CREATE INDEX IX_InvoiceItems_InvoiceId ON InvoiceItems(InvoiceId);
        """;

    public static readonly IReadOnlyList<MigrationDefinition> All = new MigrationDefinition[]
    {
        new(
            Version: 1,
            Description: "001_InitialAuth",
            UpSql: InitialAuthSql,
            TargetObjects: new[]
            {
                "Users",
                "IX_Users_Username"
            }),
        new(
            Version: 2,
            Description: "002_MedicineCatalogueAndBatches",
            UpSql: MedicineCatalogueAndBatchesSql,
            TargetObjects: new[]
            {
                "Medicines",
                "Batches",
                "StockAdjustments",
                "IX_Medicines_Name",
                "IX_Medicines_GenericName",
                "IX_Medicines_Category",
                "UX_Medicines_Barcode",
                "IX_Batches_MedicineId",
                "IX_Batches_BatchCode",
                "IX_Batches_ExpiryDate",
                "IX_Batches_IsActive",
                "UX_Batches_Id_MedicineId",
                "IX_StockAdjustments_BatchId",
                "IX_StockAdjustments_MedicineId",
                "IX_StockAdjustments_UserId",
                "IX_StockAdjustments_Timestamp",
                "TR_StockAdjustments_PreventUpdate",
                "TR_StockAdjustments_PreventDelete"
            }),
        new(
            Version: 3,
            Description: "003_Suppliers",
            UpSql: SuppliersSql,
            TargetObjects: new[]
            {
                "Suppliers",
                "IX_Suppliers_Name",
                "IX_Suppliers_PanNumber",
                "IX_Suppliers_IsActive"
            }),
        new(
            Version: 4,
            Description: "004_InvoicesAndSalesHistory",
            UpSql: InvoicesAndSalesHistorySql,
            TargetObjects: new[]
            {
                "Invoices",
                "InvoiceItems",
                "IX_Invoices_InvoiceNumber",
                "IX_Invoices_CreatedAt",
                "IX_Invoices_UserId",
                "IX_InvoiceItems_InvoiceId"
            })
    };
}
