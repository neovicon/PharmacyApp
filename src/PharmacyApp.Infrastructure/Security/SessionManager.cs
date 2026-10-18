using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Authentication;
using PharmacyApp.Application.Common;
using PharmacyApp.Domain.Enums;

namespace PharmacyApp.Infrastructure.Security;

/// <summary>
/// Thread-safe in-memory manager for session lifecycle and current user identity.
/// Guarantees that sensitive user properties are inaccessible while locked or logged out.
/// </summary>
public sealed class SessionManager : ISessionManager
{
    private readonly object _lock = new();
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;

    private UserSessionDto? _currentUser;
    private SessionState _state = SessionState.Unauthenticated;

    public SessionState State
    {
        get { lock (_lock) return _state; }
        private set { lock (_lock) _state = value; }
    }

    public UserSessionDto? CurrentUser
    {
        get { lock (_lock) return _currentUser; }
    }

    public Guid UserId
    {
        get
        {
            lock (_lock)
            {
                return (_state == SessionState.Active && _currentUser != null)
                    ? _currentUser.UserId
                    : Guid.Empty;
            }
        }
    }

    public string Username
    {
        get
        {
            lock (_lock)
            {
                return (_state == SessionState.Active && _currentUser != null)
                    ? _currentUser.Username
                    : string.Empty;
            }
        }
    }

    public string DisplayName
    {
        get
        {
            lock (_lock)
            {
                return (_state == SessionState.Active && _currentUser != null)
                    ? _currentUser.DisplayName
                    : string.Empty;
            }
        }
    }

    public UserRole Role
    {
        get
        {
            lock (_lock)
            {
                return (_state == SessionState.Active && _currentUser != null)
                    ? _currentUser.Role
                    : UserRole.Cashier;
            }
        }
    }

    public bool IsAuthenticated
    {
        get
        {
            lock (_lock)
            {
                return _state == SessionState.Active && _currentUser != null;
            }
        }
    }

    public event EventHandler<SessionStateChangedEventArgs>? SessionStateChanged;

    public SessionManager(IUserRepository userRepository, IPasswordHasher passwordHasher)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
    }

    public void StartSession(UserSessionDto user)
    {
        ArgumentNullException.ThrowIfNull(user);

        lock (_lock)
        {
            _currentUser = user;
            _state = SessionState.Active;
        }

        RaiseSessionStateChanged(SessionState.Active, user);
    }

    public void LockSession()
    {
        UserSessionDto? user;
        lock (_lock)
        {
            if (_state != SessionState.Active || _currentUser == null)
                return;

            _state = SessionState.Locked;
            user = _currentUser;
        }

        RaiseSessionStateChanged(SessionState.Locked, user);
    }

    public async Task<Result> UnlockSessionAsync(string password, CancellationToken cancellationToken = default)
    {
        UserSessionDto? currentUser;
        lock (_lock)
        {
            if (_state != SessionState.Locked || _currentUser == null)
                return Result.Failure("Session is not in a locked state.");

            currentUser = _currentUser;
        }

        if (string.IsNullOrEmpty(password))
            return Result.Failure("Password is required.");

        var user = await _userRepository.GetByIdAsync(currentUser.UserId, cancellationToken);
        if (user is null || !user.IsActive)
            return Result.Failure("User account is inactive or no longer exists.");

        bool isPasswordValid = _passwordHasher.VerifyPassword(password, user.PasswordHash);
        if (!isPasswordValid)
            return Result.Failure("Invalid password.");

        lock (_lock)
        {
            _state = SessionState.Active;
        }

        RaiseSessionStateChanged(SessionState.Active, currentUser);
        return Result.Success();
    }

    public void TerminateSession()
    {
        lock (_lock)
        {
            _currentUser = null;
            _state = SessionState.Unauthenticated;
        }

        RaiseSessionStateChanged(SessionState.Unauthenticated, null);
    }

    private void RaiseSessionStateChanged(SessionState state, UserSessionDto? user)
    {
        SessionStateChanged?.Invoke(this, new SessionStateChangedEventArgs(state, user));
    }
}
