using Microsoft.Extensions.DependencyInjection;
using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Common;
using PharmacyApp.Application.Suppliers;
using PharmacyApp.Application.Suppliers.Commands;
using PharmacyApp.Application.Suppliers.Queries;
using PharmacyApp.Infrastructure;
using Xunit;

namespace PharmacyApp.Infrastructure.Tests;

public class InfrastructureServiceExtensionsTests
{
    [Fact]
    public void AddInfrastructure_RegistersExpectedSingletons()
    {
        var services = new ServiceCollection();
        services.AddInfrastructure();

        using var provider = services.BuildServiceProvider();

        var pathProvider1 = provider.GetService<IAppPathProvider>();
        var pathProvider2 = provider.GetService<IAppPathProvider>();
        var dateTimeProvider1 = provider.GetService<IDateTimeProvider>();
        var dateTimeProvider2 = provider.GetService<IDateTimeProvider>();

        Assert.NotNull(pathProvider1);
        Assert.NotNull(dateTimeProvider1);

        // Verify Singleton lifetime
        Assert.Same(pathProvider1, pathProvider2);
        Assert.Same(dateTimeProvider1, dateTimeProvider2);

        // Verify Authentication & Session Services
        var passwordHasher = provider.GetService<IPasswordHasher>();
        var userRepo = provider.GetService<IUserRepository>();
        var sessionManager = provider.GetService<ISessionManager>();
        var currentUserService = provider.GetService<ICurrentUserService>();
        var idleTimer = provider.GetService<IIdleTimerService>();
        var throttleService = provider.GetService<ILoginThrottleService>();
        var backupService = provider.GetService<PharmacyApp.Infrastructure.Persistence.ISqliteBackupService>();
        var dbInitializer = provider.GetService<PharmacyApp.Infrastructure.Persistence.SqliteDatabaseInitializer>();

        Assert.NotNull(passwordHasher);
        Assert.NotNull(userRepo);
        Assert.NotNull(sessionManager);
        Assert.NotNull(currentUserService);
        Assert.Same(sessionManager, currentUserService);
        Assert.NotNull(idleTimer);
        Assert.NotNull(throttleService);
        Assert.NotNull(backupService);
        Assert.NotNull(dbInitializer);

        // Verify Repositories & Transaction Runner
        var medicineRepo = provider.GetService<IMedicineRepository>();
        var batchRepo = provider.GetService<IBatchRepository>();
        var adjustmentRepo = provider.GetService<IStockAdjustmentRepository>();
        var transactionRunner = provider.GetService<ITransactionRunner>();

        Assert.NotNull(medicineRepo);
        Assert.NotNull(batchRepo);
        Assert.NotNull(adjustmentRepo);
        Assert.NotNull(transactionRunner);

        Assert.Same(medicineRepo, provider.GetService<IMedicineRepository>());
        Assert.Same(batchRepo, provider.GetService<IBatchRepository>());
        Assert.Same(adjustmentRepo, provider.GetService<IStockAdjustmentRepository>());
        Assert.Same(transactionRunner, provider.GetService<ITransactionRunner>());

        // Verify Command Handlers
        var createAdminHandler = provider.GetService<ICommandHandler<PharmacyApp.Application.Authentication.Commands.CreateInitialAdminCommand, Guid>>();
        var loginHandler = provider.GetService<ICommandHandler<PharmacyApp.Application.Authentication.Commands.LoginCommand, PharmacyApp.Application.Authentication.UserSessionDto>>();

        Assert.NotNull(createAdminHandler);
        Assert.NotNull(loginHandler);

        // Verify Medicine Validator, Command & Query Handlers
        var medicineValidator = provider.GetService<PharmacyApp.Application.Medicines.IMedicineValidator>();
        var createMedicineHandler = provider.GetService<ICommandHandler<PharmacyApp.Application.Medicines.Commands.CreateMedicineCommand, Guid>>();
        var updateMedicineHandler = provider.GetService<ICommandHandler<PharmacyApp.Application.Medicines.Commands.UpdateMedicineCommand>>();
        var getByIdHandler = provider.GetService<IQueryHandler<PharmacyApp.Application.Medicines.Queries.GetMedicineByIdQuery, PharmacyApp.Application.Medicines.MedicineDto>>();
        var getByBarcodeHandler = provider.GetService<IQueryHandler<PharmacyApp.Application.Medicines.Queries.GetMedicineByBarcodeQuery, PharmacyApp.Application.Medicines.MedicineDto>>();
        var searchMedicinesHandler = provider.GetService<IQueryHandler<PharmacyApp.Application.Medicines.Queries.SearchMedicinesQuery, IReadOnlyList<PharmacyApp.Application.Medicines.MedicineDto>>>();
        var listMedicinesHandler = provider.GetService<IQueryHandler<PharmacyApp.Application.Medicines.Queries.ListMedicinesQuery, IReadOnlyList<PharmacyApp.Application.Medicines.MedicineDto>>>();

        Assert.NotNull(medicineValidator);
        Assert.NotNull(createMedicineHandler);
        Assert.NotNull(updateMedicineHandler);
        Assert.NotNull(getByIdHandler);
        Assert.NotNull(getByBarcodeHandler);
        Assert.NotNull(searchMedicinesHandler);
        Assert.NotNull(listMedicinesHandler);

        // Verify Settings & User Services and Handlers
        var profileService = provider.GetService<PharmacyApp.Application.Settings.IPharmacyProfileService>();
        var dbManagementService = provider.GetService<PharmacyApp.Application.Abstractions.IDatabaseManagementService>();
        var getDbStatusHandler = provider.GetService<IQueryHandler<PharmacyApp.Application.Settings.Queries.GetDatabaseStatusQuery, PharmacyApp.Application.Settings.DatabaseStatusDto>>();
        var createBackupHandler = provider.GetService<ICommandHandler<PharmacyApp.Application.Settings.Commands.CreateManualBackupCommand, PharmacyApp.Application.Settings.BackupInfoDto>>();
        var listUsersHandler = provider.GetService<IQueryHandler<PharmacyApp.Application.Users.Queries.ListUsersQuery, IReadOnlyList<PharmacyApp.Application.Users.UserDto>>>();
        var createUserHandler = provider.GetService<ICommandHandler<PharmacyApp.Application.Users.Commands.CreateUserCommand, Guid>>();
        var toggleUserHandler = provider.GetService<ICommandHandler<PharmacyApp.Application.Users.Commands.ToggleUserActiveCommand>>();
        var resetPasswordHandler = provider.GetService<ICommandHandler<PharmacyApp.Application.Users.Commands.ResetUserPasswordCommand>>();

        Assert.NotNull(profileService);
        Assert.NotNull(dbManagementService);
        Assert.NotNull(getDbStatusHandler);
        Assert.NotNull(createBackupHandler);
        Assert.NotNull(listUsersHandler);
        Assert.NotNull(createUserHandler);
        Assert.NotNull(toggleUserHandler);
        Assert.NotNull(resetPasswordHandler);

        // Verify Supplier Repository and Handlers
        var supplierRepo = provider.GetService<ISupplierRepository>();
        var listSuppliersHandler = provider.GetService<IQueryHandler<ListSuppliersQuery, IReadOnlyList<SupplierDto>>>();
        var createSupplierHandler = provider.GetService<ICommandHandler<CreateSupplierCommand, Guid>>();
        var toggleSupplierHandler = provider.GetService<ICommandHandler<ToggleSupplierActiveCommand>>();

        Assert.NotNull(supplierRepo);
        Assert.NotNull(listSuppliersHandler);
        Assert.NotNull(createSupplierHandler);
        Assert.NotNull(toggleSupplierHandler);
    }
}
