namespace PharmacyApp.Domain.Enums;

/// <summary>
/// Supported payment channels for POS dispensing checkout.
/// </summary>
public enum PaymentMethod
{
    Cash = 1,
    Card = 2,
    DigitalWalletQr = 3,
    CreditOrInsurance = 4
}
