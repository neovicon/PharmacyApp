namespace PharmacyApp.Domain.Common;

/// <summary>
/// Base class for all domain entities. Uses Guid as primary key for
/// global uniqueness — required for future multi-device synchronization.
/// </summary>
public abstract class EntityBase
{
    public Guid Id { get; protected set; } = Guid.NewGuid();

    /// <summary>UTC timestamp set when the entity is first persisted.</summary>
    public DateTimeOffset CreatedAt { get; protected set; }

    /// <summary>UTC timestamp updated on every mutation.</summary>
    public DateTimeOffset UpdatedAt { get; protected set; }

    // Row version for optimistic concurrency (populated by EF Core in Milestone 1).
    public uint Version { get; protected set; }
}
