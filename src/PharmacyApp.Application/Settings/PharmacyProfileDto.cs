namespace PharmacyApp.Application.Settings;

/// <summary>
/// Pharmacy business profile and system preferences.
/// </summary>
public sealed class PharmacyProfileDto
{
    public string StoreName { get; set; } = "Community Care Pharmacy";
    public string Address { get; set; } = "123 Health Ave, Kathmandu";
    public string Phone { get; set; } = "+977-1-4412345";
    public string TaxPanNumber { get; set; } = "PAN-601234567";
    public string ReceiptFooterMessage { get; set; } = "Thank you for trusting Community Care Pharmacy. Wishing you good health!";
    public int AutoLockMinutes { get; set; } = 15;
}
