namespace PharmacyApp.Domain.Enums;

/// <summary>
/// User roles that control access to application features.
/// Keep roles coarse-grained until business requirements demand finer control.
/// </summary>
public enum UserRole
{
    /// <summary>Full access: user management, settings, reports, voids, backups.</summary>
    Admin = 0,

    /// <summary>Dispensing, purchasing, stock adjustments, returns.</summary>
    Pharmacist = 1,

    /// <summary>Sales and basic stock enquiries only.</summary>
    Cashier = 2,
}
