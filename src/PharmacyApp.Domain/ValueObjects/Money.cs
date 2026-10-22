namespace PharmacyApp.Domain.ValueObjects;

/// <summary>
/// Represents an immutable monetary value in Nepalese Rupees (NPR).
/// 
/// Design & Persistence Boundaries:
/// - Uses <see cref="decimal"/> in the domain to guarantee exact financial calculations without floating-point drift.
/// - Precision is strictly defined to 2 decimal places (Paisa precision: 1 NPR = 100 Paisa).
/// - Unrounded inputs with more than 2 decimal places are rejected explicitly rather than silently rounded.
/// - When persisted to SQLite in future milestones, monetary values should be stored as integer Paisa
///   (or documented fixed-point text) to eliminate SQLite REAL floating-point imprecision.
/// </summary>
public readonly record struct Money : IComparable<Money>, IEquatable<Money>
{
    public const string DefaultCurrency = "NPR";
    public const int DecimalPlaces = 2;
    public const long PaisaPerRupee = 100;

    public decimal Amount { get; }
    public string Currency { get; }

    public static Money Zero => new(0m, DefaultCurrency);

    private Money(decimal amount, string currency)
    {
        Amount = amount;
        Currency = currency;
    }

    /// <summary>
    /// Creates a <see cref="Money"/> instance from a Nepalese Rupee amount.
    /// Rejects negative amounts and amounts with fractional paisa (more than 2 decimal places).
    /// </summary>
    public static Money FromNpr(decimal amount)
    {
        if (amount < 0m)
            throw new ArgumentOutOfRangeException(nameof(amount), amount, "Monetary amount cannot be negative.");

        if (decimal.Round(amount, DecimalPlaces) != amount)
            throw new ArgumentException(
                $"Amount '{amount}' has more than {DecimalPlaces} decimal places. Precision is limited to paisa (2 decimal places). Rounding must be performed explicitly by the caller.",
                nameof(amount));

        return new Money(amount, DefaultCurrency);
    }

    /// <summary>
    /// Creates a <see cref="Money"/> instance from an integer count of Paisa (1 NPR = 100 Paisa).
    /// Used primarily when reconstituting monetary amounts from integer-backed persistence.
    /// </summary>
    public static Money FromPaisa(long paisa)
    {
        if (paisa < 0)
            throw new ArgumentOutOfRangeException(nameof(paisa), paisa, "Paisa cannot be negative.");

        decimal amount = (decimal)paisa / PaisaPerRupee;
        return new Money(amount, DefaultCurrency);
    }

    /// <summary>
    /// Explicitly rounds a raw fractional decimal (e.g. from margin or tax calculation)
    /// to paisa precision and returns a valid <see cref="Money"/> instance.
    /// </summary>
    public static Money RoundToPaisa(decimal rawAmount, MidpointRounding rounding = MidpointRounding.AwayFromZero)
    {
        if (rawAmount < 0m)
            throw new ArgumentOutOfRangeException(nameof(rawAmount), rawAmount, "Monetary amount cannot be negative.");

        decimal rounded = decimal.Round(rawAmount, DecimalPlaces, rounding);
        return new Money(rounded, DefaultCurrency);
    }

    /// <summary>
    /// Converts the NPR amount to total integer Paisa for lossless storage or minor-unit calculations.
    /// </summary>
    public long ToPaisa()
    {
        return (long)(Amount * PaisaPerRupee);
    }

    public static Money operator +(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        return new Money(left.Amount + right.Amount, left.Currency);
    }

    public static Money operator -(Money left, Money right)
    {
        EnsureSameCurrency(left, right);
        if (left.Amount < right.Amount)
            throw new InvalidOperationException($"Resulting monetary amount cannot be negative ({left} - {right}).");

        return new Money(left.Amount - right.Amount, left.Currency);
    }

    public static Money operator *(Money money, int quantity)
    {
        if (quantity < 0)
            throw new ArgumentOutOfRangeException(nameof(quantity), quantity, "Quantity cannot be negative.");

        return new Money(money.Amount * quantity, money.Currency);
    }

    public static Money operator *(int quantity, Money money) => money * quantity;

    public int CompareTo(Money other)
    {
        EnsureSameCurrency(this, other);
        return Amount.CompareTo(other.Amount);
    }

    public static bool operator <(Money left, Money right) => left.CompareTo(right) < 0;
    public static bool operator <=(Money left, Money right) => left.CompareTo(right) <= 0;
    public static bool operator >(Money left, Money right) => left.CompareTo(right) > 0;
    public static bool operator >=(Money left, Money right) => left.CompareTo(right) >= 0;

    public override string ToString() => $"{Currency} {Amount:F2}";

    private static void EnsureSameCurrency(Money a, Money b)
    {
        if (!string.Equals(a.Currency, b.Currency, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Cannot perform operations across different currencies: '{a.Currency}' and '{b.Currency}'.");
    }
}
