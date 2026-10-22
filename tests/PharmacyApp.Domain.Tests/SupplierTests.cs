using PharmacyApp.Domain.Entities;
using Xunit;

namespace PharmacyApp.Domain.Tests;

public class SupplierTests
{
    private readonly DateTimeOffset _fixedTime = new(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_WithValidParameters_InitializesCorrectly()
    {
        var supplier = Supplier.Create(
            name: "Deurali-Janta Pharmaceuticals",
            contactPerson: "Rajesh Shrestha",
            phone: "+977-1-4351234",
            email: "info@djpl.com.np",
            panNumber: "PAN-100234567",
            address: "Dhapasi, Kathmandu",
            now: _fixedTime);

        Assert.NotEqual(Guid.Empty, supplier.Id);
        Assert.Equal("Deurali-Janta Pharmaceuticals", supplier.Name);
        Assert.Equal("Rajesh Shrestha", supplier.ContactPerson);
        Assert.Equal("+977-1-4351234", supplier.Phone);
        Assert.Equal("info@djpl.com.np", supplier.Email);
        Assert.Equal("PAN-100234567", supplier.PanNumber);
        Assert.Equal("Dhapasi, Kathmandu", supplier.Address);
        Assert.True(supplier.IsActive);
        Assert.Equal(1u, supplier.Version);
        Assert.Equal(_fixedTime, supplier.CreatedAt);
        Assert.Equal(_fixedTime, supplier.UpdatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Create_WithEmptyName_ThrowsArgumentException(string? invalidName)
    {
        Assert.Throws<ArgumentException>(() => Supplier.Create(
            name: invalidName!,
            contactPerson: null,
            phone: null,
            email: null,
            panNumber: null,
            address: null,
            now: _fixedTime));
    }

    [Fact]
    public void UpdateDetails_UpdatesFieldsAndIncrementsVersion()
    {
        var supplier = Supplier.Create("Orig Name", null, null, null, null, null, _fixedTime);
        var updateTime = _fixedTime.AddHours(2);

        supplier.UpdateDetails("Updated Pharma Ltd", "Binod Rai", "9841000000", "binod@pharma.com", "PAN-999", "Biratnagar", updateTime);

        Assert.Equal("Updated Pharma Ltd", supplier.Name);
        Assert.Equal("Binod Rai", supplier.ContactPerson);
        Assert.Equal("9841000000", supplier.Phone);
        Assert.Equal("binod@pharma.com", supplier.Email);
        Assert.Equal("PAN-999", supplier.PanNumber);
        Assert.Equal("Biratnagar", supplier.Address);
        Assert.Equal(2u, supplier.Version);
        Assert.Equal(updateTime, supplier.UpdatedAt);
    }

    [Fact]
    public void DeactivateAndActivate_TogglesIsActiveAndIncrementsVersion()
    {
        var supplier = Supplier.Create("Pharma", null, null, null, null, null, _fixedTime);

        supplier.Deactivate(_fixedTime.AddMinutes(5));
        Assert.False(supplier.IsActive);
        Assert.Equal(2u, supplier.Version);

        supplier.Activate(_fixedTime.AddMinutes(10));
        Assert.True(supplier.IsActive);
        Assert.Equal(3u, supplier.Version);
    }
}
