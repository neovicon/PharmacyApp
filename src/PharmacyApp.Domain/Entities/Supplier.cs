using PharmacyApp.Domain.Common;

namespace PharmacyApp.Domain.Entities;

/// <summary>
/// Represents a wholesale pharmaceutical supplier, distributor, or manufacturer.
/// </summary>
public sealed class Supplier : EntityBase
{
    public string Name { get; private set; }
    public string? ContactPerson { get; private set; }
    public string? Phone { get; private set; }
    public string? Email { get; private set; }
    public string? PanNumber { get; private set; }
    public string? Address { get; private set; }
    public bool IsActive { get; private set; }

    private Supplier(
        Guid id,
        string name,
        string? contactPerson,
        string? phone,
        string? email,
        string? panNumber,
        string? address,
        bool isActive,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        uint version)
    {
        Id = id;
        Name = name;
        ContactPerson = contactPerson;
        Phone = phone;
        Email = email;
        PanNumber = panNumber;
        Address = address;
        IsActive = isActive;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        Version = version;
    }

    public static Supplier Create(
        string name,
        string? contactPerson,
        string? phone,
        string? email,
        string? panNumber,
        string? address,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Supplier name cannot be empty.", nameof(name));

        return new Supplier(
            id: Guid.NewGuid(),
            name: name.Trim(),
            contactPerson: string.IsNullOrWhiteSpace(contactPerson) ? null : contactPerson.Trim(),
            phone: string.IsNullOrWhiteSpace(phone) ? null : phone.Trim(),
            email: string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant(),
            panNumber: string.IsNullOrWhiteSpace(panNumber) ? null : panNumber.Trim(),
            address: string.IsNullOrWhiteSpace(address) ? null : address.Trim(),
            isActive: true,
            createdAt: now,
            updatedAt: now,
            version: 1);
    }

    public static Supplier Load(
        Guid id,
        string name,
        string? contactPerson,
        string? phone,
        string? email,
        string? panNumber,
        string? address,
        bool isActive,
        DateTimeOffset createdAt,
        DateTimeOffset updatedAt,
        uint version)
    {
        return new Supplier(id, name, contactPerson, phone, email, panNumber, address, isActive, createdAt, updatedAt, version);
    }

    public void UpdateDetails(
        string name,
        string? contactPerson,
        string? phone,
        string? email,
        string? panNumber,
        string? address,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Supplier name cannot be empty.", nameof(name));

        Name = name.Trim();
        ContactPerson = string.IsNullOrWhiteSpace(contactPerson) ? null : contactPerson.Trim();
        Phone = string.IsNullOrWhiteSpace(phone) ? null : phone.Trim();
        Email = string.IsNullOrWhiteSpace(email) ? null : email.Trim().ToLowerInvariant();
        PanNumber = string.IsNullOrWhiteSpace(panNumber) ? null : panNumber.Trim();
        Address = string.IsNullOrWhiteSpace(address) ? null : address.Trim();
        UpdatedAt = now;
        Version++;
    }

    public void Deactivate(DateTimeOffset now)
    {
        IsActive = false;
        UpdatedAt = now;
        Version++;
    }

    public void Activate(DateTimeOffset now)
    {
        IsActive = true;
        UpdatedAt = now;
        Version++;
    }
}
