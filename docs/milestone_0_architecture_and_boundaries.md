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
5. **Session & Current User Context**:
   - `ICurrentUserService` provides `UserId`, `Username`, `DisplayName`, `Role`, and `IsAuthenticated`.
   - Business views and use cases check `IsAuthenticated` before executing.
6. **Idle Lock & Work Preservation**:
   - Configurable idle timeout (default 15 minutes, implemented via `IIdleTimerService`).
   - Window input activity tracking (`PointerMoved`, `PointerPressed`, `KeyDown`) resets the inactivity countdown without invasive global system hooks.
   - Locking displays a modal `LockOverlayView` requiring password re-entry; it preserves background ViewModels, navigation state, and form entries.
7. **Storage Staging**:
   - Milestone 0.5 authentication storage is implemented directly with durable local SQLite (`Microsoft.Data.Sqlite`) in `{DataDirectory}/pharmacy.db`.
   - WAL (Write-Ahead Logging) journal mode is enabled for durability and concurrent responsiveness.

---

## 7. Milestone 0.5 Authentication & Session Foundation

### Architecture & Layer Boundaries
- **Domain**: `User` entity (Guid Id, lowercase username, displayName, BCrypt passwordHash, UserRole, isActive, CreatedAt, UpdatedAt, Version).
- **Application**:
  - `IPasswordHasher`: Password hashing and verification abstraction.
  - `IUserRepository`: Persistent account repository contract.
  - `ISessionManager` & `ICurrentUserService`: Session state machine (`Unauthenticated`, `Active`, `Locked`) and identity provider.
  - `IIdleTimerService`: Inactivity timer contract with activity reset hooks.
  - `PasswordPolicy`: Sensible policy enforcing $\ge 8$ chars, $\le 128$ chars, mix of letters and digits/symbols, and confirmation match.
  - `CreateInitialAdminCommand` & `CreateInitialAdminCommandHandler`: First-run setup use case guarding against duplicate administrator creation.
  - `LoginCommand` & `LoginCommandHandler`: Direct authentication use case returning generic error messages to eliminate username enumeration.
- **Infrastructure**:
  - `BCryptPasswordHasher`: BCrypt.Net-Next implementation with work factor 12 and unique cryptographic salts.
  - `SqliteDatabaseInitializer`: Automatic schema creation and index verification on application startup.
  - `SqliteUserRepository`: Parameterized SQL queries against SQLite database at `{DataDirectory}/pharmacy.db`.
  - `SessionManager`: Thread-safe session coordinator masking user identity when locked or logged out. Re-verifies password against database on unlock.
  - `IdleTimerService`: Deterministic timer consuming `IDateTimeProvider` for testability without real-time delays.
- **Desktop**:
  - `FirstRunSetupView` & `FirstRunSetupViewModel`: Displayed on first boot when 0 users exist.
  - `LoginView` & `LoginViewModel`: Standard authentication screen.
  - `LockOverlayView` & `LockOverlayViewModel`: Modal lock screen preserving background page state.
  - `ShellViewModel`: Coordinates UI states (`FirstRunSetup`, `Login`, `Authenticated`, `Locked`).
  - `ShellWindow`: Hooks user pointer and key activity to automatically refresh the idle timer.

### Security Choices & Considerations
1. **Password Hashing**: `BCrypt.Net-Next` (v4.2.0) with work factor 12. Generates unique 128-bit salt per password; resistant to rainbow tables and GPU brute-force attacks. Plaintext passwords are never logged or stored.
2. **Local Storage**: `Microsoft.Data.Sqlite` (v10.0.13). Lightweight, self-contained, ACID-compliant local storage located in the OS application data folder (`~/.local/share/PharmacyApp` on Linux, `%LOCALAPPDATA%\PharmacyApp` on Windows). Never placed in the source tree.
3. **No Information Leaks**: Login failures yield identical generic error messages ("Invalid username or password") regardless of whether the username exists, is inactive, or the password is wrong.
4. **No Protected Shell Flash**: `ShellWindow.axaml` completely hides the protected navigation shell grid when unauthenticated or during initial setup.

---

## 8. Persistence & Multi-Device Sync Boundaries (Milestone 1+)

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

## 9. Barcode Scanner Handling Strategy (Milestone 2+)

1. **Input Mode**:
   - Hardware USB/Bluetooth scanners operate in HID keyboard emulation mode, outputting characters followed by an `Enter` keystroke.
2. **Scanner Abstraction**:
   - Scans will be handled through a focused text buffer abstraction.
   - Keystrokes will **not** be intercepted globally when focus is in a password, PIN, or sensitive text box.
   - Manual search and SKU/barcode entry will always remain available as a fallback.
3. **Validation**:
   - Scanned barcodes are normalized and validated before triggering medicine lookup.

---

## 10. Verification & Test Evidence

### Build Status
```bash
dotnet restore PharmacyApp.slnx
dotnet build PharmacyApp.slnx --no-restore
# 8 projects: 0 Warning(s), 0 Error(s)
```

### Automated Tests
```bash
dotnet test PharmacyApp.slnx
# Passed! - Failed: 0, Passed: 23 - PharmacyApp.Domain.Tests.dll
# Passed! - Failed: 0, Passed: 33 - PharmacyApp.Application.Tests.dll
# Passed! - Failed: 0, Passed: 34 - PharmacyApp.Infrastructure.Tests.dll
# Passed! - Failed: 0, Passed: 38 - PharmacyApp.Desktop.Tests.dll
# Total: 128 passed, 0 failed, 0 skipped
```

### Runtime Verification
- Executed compiled binary under Linux Wayland/X11 (`DOTNET_ROOT=/home/daniel/.dotnet src/PharmacyApp.Desktop/bin/Debug/net10.0/PharmacyApp.Desktop`).
- Process started cleanly, verified 0 existing users in `pharmacy.db`, and displayed `FirstRunSetupView` without any protected shell flicker.
- Structured log entry written to `~/.local/share/PharmacyApp/Logs/pharmacy-20261018.log`.
- Executed compiled binary under Linux Wayland/X11 (`DISPLAY=:0`).
- Process started cleanly, registered all DI services, initialized Avalonia desktop lifetime, and wrote structured log entry:
  `2026-10-18 17:33:31.213 +05:45 [INF] PharmacyApp Desktop starting. Version=0.1.0.0 Environment=Development` to `~/.local/share/PharmacyApp/Logs/pharmacy-20261018.log`.
