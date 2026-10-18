using PharmacyApp.Application.Authentication;
using PharmacyApp.Application.Common;

namespace PharmacyApp.Application.Abstractions;

public enum SessionState
{
    Unauthenticated,
    Active,
    Locked
}

public sealed class SessionStateChangedEventArgs : EventArgs
{
    public SessionState State { get; }
    public UserSessionDto? User { get; }

    public SessionStateChangedEventArgs(SessionState state, UserSessionDto? user)
    {
        State = state;
        User = user;
    }
}

/// <summary>
/// Manages the in-memory lifecycle of authenticated sessions,
/// locking, unlocking, and active user identity.
/// </summary>
public interface ISessionManager : ICurrentUserService
{
    SessionState State { get; }
    bool IsLocked => State == SessionState.Locked;
    UserSessionDto? CurrentUser { get; }

    void StartSession(UserSessionDto user);
    void LockSession();
    Task<Result> UnlockSessionAsync(string password, CancellationToken cancellationToken = default);
    void TerminateSession();

    event EventHandler<SessionStateChangedEventArgs>? SessionStateChanged;
}
