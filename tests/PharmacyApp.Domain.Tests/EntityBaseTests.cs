using PharmacyApp.Domain.Common;
using Xunit;

namespace PharmacyApp.Domain.Tests;

public class EntityBaseTests
{
    private sealed class TestEntity : EntityBase
    {
        public TestEntity() { }

        public TestEntity(Guid id)
        {
            Id = id;
        }

        public void SetTimestamps(DateTimeOffset created, DateTimeOffset updated)
        {
            CreatedAt = created;
            UpdatedAt = updated;
        }

        public void SetVersion(uint version)
        {
            Version = version;
        }
    }

    [Fact]
    public void NewEntity_InitializesWithNonEmptyGuid()
    {
        var entity = new TestEntity();

        Assert.NotEqual(Guid.Empty, entity.Id);
    }

    [Fact]
    public void TwoEntities_HaveDistinctIdsByDefault()
    {
        var entity1 = new TestEntity();
        var entity2 = new TestEntity();

        Assert.NotEqual(entity1.Id, entity2.Id);
    }

    [Fact]
    public void SetTimestamps_PreservesGivenValues()
    {
        var entity = new TestEntity();
        var created = new DateTimeOffset(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);
        var updated = created.AddMinutes(5);

        entity.SetTimestamps(created, updated);

        Assert.Equal(created, entity.CreatedAt);
        Assert.Equal(updated, entity.UpdatedAt);
    }

    [Fact]
    public void SetVersion_PreservesConcurrencyToken()
    {
        var entity = new TestEntity();
        entity.SetVersion(42);

        Assert.Equal(42u, entity.Version);
    }
}
