using PharmacyApp.Application.Abstractions;
using PharmacyApp.Application.Common;

namespace PharmacyApp.Application.Users.Queries;

/// <summary>
/// Query to list all system users.
/// </summary>
public sealed record ListUsersQuery;

/// <summary>
/// Handles listing all user accounts ordered by username.
/// </summary>
public sealed class ListUsersQueryHandler : IQueryHandler<ListUsersQuery, IReadOnlyList<UserDto>>
{
    private readonly IUserRepository _userRepository;

    public ListUsersQueryHandler(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<Result<IReadOnlyList<UserDto>>> HandleAsync(ListUsersQuery query, CancellationToken cancellationToken = default)
    {
        var users = await _userRepository.ListAsync(cancellationToken);
        var dtos = users.Select(UserDto.FromDomain).ToList();
        return Result.Success<IReadOnlyList<UserDto>>(dtos);
    }
}
