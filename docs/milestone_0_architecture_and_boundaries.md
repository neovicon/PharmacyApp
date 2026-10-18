# PharmacyApp — Milestone 0 Foundation & Architecture Boundaries

## 1. Executive Summary & Status

Milestone 0 establishes the architectural and engineering foundation for **PharmacyApp**, a cross-platform (Linux dev / Windows production) desktop pharmacy management application built with .NET 10, Avalonia 12, CommunityToolkit.Mvvm, and Microsoft.Extensions.Hosting.

All 8 solution projects compile cleanly with **0 errors and 0 warnings**.
A full test suite of **42 unit and integration tests** passes across Domain, Application, Infrastructure, and Desktop layers.

---

## 2. Multi-Project Solution Structure

```
PharmacyApp/
├── PharmacyApp.slnx                     # SDK-style .NET 10 solution
├── .gitignore                           # Standard .NET gitignore
├── docs/                                # Architecture & boundary specifications
├── legacy_starter_backup/               # Archived unmodified template files
├── src/
│   ├── PharmacyApp.Domain/              # Zero-dependency domain models & contracts
│   │   ├── Common/EntityBase.cs         # Guid PK, UTC timestamps, concurrency Version
│   │   └── Enums/UserRole.cs            # Admin, Pharmacist, Cashier
│   ├── PharmacyApp.Application/         # Use cases, interfaces, DTOs (no UI/DB dependencies)
│   │   ├── Abstractions/
│   │   │   ├── IAppPathProvider.cs      # Portable writable filesystem abstraction
│   │   │   ├── ICurrentUserService.cs   # Authenticated session user contract
│   │   │   ├── IDateTimeProvider.cs     # UTC wall-clock time provider
│   │   │   └── IDialogService.cs        # UI dialog & confirmation abstraction
│   │   └── Common/
│   │       ├── ICommandHandler.cs       # Direct handler injection (no MediatR)
│   │       ├── IQueryHandler.cs         # Direct query handler injection
│   │       └── Result.cs                # Discriminated Result and Result<T> types
│   ├── PharmacyApp.Infrastructure/      # File system, Serilog, OS integrations
│   │   ├── Services/
│   │   │   ├── AppPathProvider.cs       # Linux XDG & Windows %LOCALAPPDATA% provider
│   │   │   └── SystemDateTimeProvider.cs# Real wall-clock provider
│   │   └── InfrastructureServiceExtensions.cs # Serilog configuration & DI registration
│   └── PharmacyApp.Desktop/             # Avalonia 12 MVVM desktop client
│       ├── Navigation/
│       │   ├── INavigationService.cs    # Sidebar page navigation contract
│       │   └── NavigationService.cs     # DI-backed page switcher
│       ├── Services/
│       │   └── DesktopDialogService.cs  # IDialogService desktop implementation
│       ├── ViewModels/                  # CommunityToolkit.Mvvm ViewModels
│       │   ├── Shell/ShellViewModel.cs  # Sidebar items and CurrentPage binding
│       │   └── Dashboard/, Medicines/, Inventory/, Purchases/, Sales/, Reports/, Settings/
│       ├── Views/                       # Avalonia AXAML user controls & ShellWindow
│       ├── ViewLocator.cs               # DI-aware ViewModel -> View resolution
│       ├── Program.cs                   # IHost bootstrap, Serilog, unhandled exception hooks
│       └── DesktopServiceExtensions.cs  # DI container registrations
└── tests/
    ├── PharmacyApp.Domain.Tests/        # EntityBase & UserRole tests (6 tests)
    ├── PharmacyApp.Application.Tests/   # Result & Abstraction tests (4 tests)
    ├── PharmacyApp.Infrastructure.Tests/# AppPathProvider & DI tests (6 tests)
    └── PharmacyApp.Desktop.Tests/       # Navigation, Shell, Selection, ViewLocator, Dialog, DI tests (26 tests)
```

---

## 3. Dependency Direction & Composition Root

Dependency rule strictly enforced:
- **Domain**: Pure C#, no external project or package dependencies.
- **Application**: References `Domain` only. Defines business abstractions and direct handlers.
- **Infrastructure**: References `Application` and `Domain`. Implements filesystem paths and logging.
- **Desktop**: References `Application` and `Infrastructure`. Serves as the composition root.
- **Tests**: Each test suite mirrors its respective layer with isolated unit tests.

### Command Handling Policy
- Direct handler injection is enforced: **No MediatR**.
- Command handlers implement `ICommandHandler<TCommand>` and return `Result` or `Result<T>`.
- Query handlers implement `IQueryHandler<TQuery, TResult>`.
- Cross-cutting concerns (such as audit logging and transactional consistency) will be applied explicitly when SQLite persistence is introduced in Milestone 1.

---

## 4. Shell, MVVM, and Navigation Design

- **Layout**: Fixed sidebar (230px) with logo, navigation links, and an auth area reserved for Milestone 0.5.
- **Navigation**: `NavigationService` manages `CurrentPage` via `NavigateTo<TViewModel>()`. ViewModels are singletons to preserve view state across menu clicks.
- **View Resolution**: `ViewLocator` replaces `ViewModel` with `View`, queries the DI container first so Views can receive injected dependencies, and falls back safely without unhandled crashes.
- **Placeholders**: All unfinished sections (`Medicines`, `Inventory`, `Purchases`, `Sales`, `Reports`, `Settings`) clearly display their placeholder status and milestone milestones without fake or misleading data.

---

## 5. Logging, Errors, and File Safety

- **Data Directory Provider (`IAppPathProvider`)**:
  - Linux: Respects `$XDG_DATA_HOME` with fallback to `~/.local/share/PharmacyApp/`.
  - Windows: Uses `%LOCALAPPDATA%\PharmacyApp\`.
  - Ensures paths are created safely on disk. Never hardcodes machine-specific usernames.
- **Structured Logging (Serilog)**:
  - Startup bootstrap logger catches startup failures.
  - Daily rolling file sink: `Logs/pharmacy-yyyyMMdd.log` with 30-day retention and async file writing.
  - Application version and environment logged on startup.
  - Sensitive data policy: credentials, passwords, PINs, tokens, and patient details are strictly forbidden from log output.
- **Application Exception Hooks**:
  - `AppDomain.CurrentDomain.UnhandledException` logs fatal unhandled exceptions.
  - `TaskScheduler.UnobservedTaskException` logs unobserved async task faults and marks them observed.

---

## 6. Milestone 0.5 Authentication Design Boundaries

Authentication is deliberately isolated to **Milestone 0.5** to ensure stability.

### Boundary Specifications:
1. **First-Run Setup**: On first application launch, the application checks if any user accounts exist. If none exist, the shell is locked to a mandatory First-Run Administrator Creation view.
2. **Zero Default Credentials**: The application will never ship with a default or hardcoded username/password.
3. **Password Security**:
   - Standard BCrypt hashing with work factor $\ge 12$.
   - Passwords and plaintext PINs are never stored, logged, or serialized.
4. **Login Flow & Brute-Force Prevention**:
   - Generic login failure messages ("Invalid username or password") to prevent username enumeration.
   - Exponential backoff / failed-attempt throttling after consecutive invalid tries.
5. **Session & Current User Context**:
   - `ICurrentUserService` provides `UserId`, `Username`, `DisplayName`, `Role`, and `IsAuthenticated`.
   - Business views and use cases check `IsAuthenticated` before executing.
6. **Idle Lock & Work Preservation**:
   - Configurable idle timeout (e.g. 5–15 minutes).
   - Locking displays a modal lock overlay requiring password/PIN re-entry; it does **not** reset ViewModels or destroy unsaved billing/form data.
7. **Storage Staging**:
   - Milestone 0.5 authentication storage will be implemented directly with SQLite in Milestone 1 (or safely isolated via an interface if implemented in Milestone 0.5).

---

## 7. Persistence & Multi-Device Sync Boundaries (Milestone 1+)

1. **Entity Identifiers**:
   - All domain entities inherit `EntityBase` using non-empty client-generated `Guid` primary keys. This guarantees ID uniqueness across multiple client registers without requiring centralized auto-increment coordination.
2. **Audit & Concurrency**:
   - `CreatedAt` and `UpdatedAt` stored as UTC (`DateTimeOffset`).
   - `Version` (uint) concurrency token for optimistic locking.
3. **No Silent Overwrites**:
   - When synchronizing or updating records, optimistic concurrency exceptions are surfaced. Stale client edits will never overwrite newer server or local modifications silently.
4. **Transactional Audit Logging**:
   - Business mutations (stock adjustments, price changes, sales voids) will write audit logs within the same database transaction in Milestone 1.

---

## 8. Barcode Scanner Handling Strategy (Milestone 2+)

1. **Input Mode**:
   - Hardware USB/Bluetooth scanners operate in HID keyboard emulation mode, outputting characters followed by an `Enter` keystroke.
2. **Scanner Abstraction**:
   - Scans will be handled through a focused text buffer abstraction.
   - Keystrokes will **not** be intercepted globally when focus is in a password, PIN, or sensitive text box.
   - Manual search and SKU/barcode entry will always remain available as a fallback.
3. **Validation**:
   - Scanned barcodes are normalized and validated before triggering medicine lookup.

---

## 9. Verification & Test Evidence

### Build Status
```bash
dotnet build
# 8 projects: 0 Warning(s), 0 Error(s)
```

### Automated Tests
```bash
dotnet test PharmacyApp.slnx
# Passed! - Failed: 0, Passed: 6  - PharmacyApp.Domain.Tests.dll
# Passed! - Failed: 0, Passed: 4  - PharmacyApp.Application.Tests.dll
# Passed! - Failed: 0, Passed: 6  - PharmacyApp.Infrastructure.Tests.dll
# Passed! - Failed: 0, Passed: 26 - PharmacyApp.Desktop.Tests.dll
# Total: 42 passed, 0 failed
```

### Runtime Verification
- Executed compiled binary under Linux Wayland/X11 (`DISPLAY=:0`).
- Process started cleanly, registered all DI services, initialized Avalonia desktop lifetime, and wrote structured log entry:
  `2026-10-18 17:33:31.213 +05:45 [INF] PharmacyApp Desktop starting. Version=0.1.0.0 Environment=Development` to `~/.local/share/PharmacyApp/Logs/pharmacy-20261018.log`.
