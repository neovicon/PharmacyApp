using PharmacyApp.Domain.ValueObjects;
using Xunit;

namespace PharmacyApp.Domain.Tests;

public class MoneyTests
{
    [Fact]
    public void FromNpr_WithValidTwoDecimalAmount_CreatesSuccessfully()
    {
        var money = Money.FromNpr(150.50m);

        Assert.Equal(150.50m, money.Amount);
        Assert.Equal("NPR", money.Currency);
        Assert.Equal("NPR 150.50", money.ToString());
    }

    [Fact]
    public void FromNpr_WithZeroAmount_CreatesSuccessfully()
    {
        var money = Money.FromNpr(0m);

        Assert.Equal(0m, money.Amount);
        Assert.Equal(Money.Zero, money);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(-100.00)]
    public void FromNpr_WithNegativeAmount_ThrowsArgumentOutOfRangeException(decimal negativeAmount)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Money.FromNpr(negativeAmount));
    }

    [Theory]
    [InlineData(10.001)]
    [InlineData(99.999)]
    [InlineData(123.4567)]
    public void FromNpr_WithMoreThanTwoDecimalPlaces_ThrowsArgumentException_WithoutSilentRounding(decimal unrounded)
    {
        var ex = Assert.Throws<ArgumentException>(() => Money.FromNpr(unrounded));
        Assert.Contains("paisa", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void FromPaisa_WithValidPaisa_ConvertsAccurately()
    {
        var money = Money.FromPaisa(15050);

        Assert.Equal(150.50m, money.Amount);
        Assert.Equal(15050, money.ToPaisa());
    }

    [Fact]
    public void FromPaisa_WithZero_ReturnsZero()
    {
        var money = Money.FromPaisa(0);

        Assert.Equal(0m, money.Amount);
        Assert.Equal(0, money.ToPaisa());
    }

    [Fact]
    public void FromPaisa_WithNegativePaisa_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Money.FromPaisa(-50));
    }

    [Fact]
    public void RoundToPaisa_ExplicitRounding_AppliesMidpointRounding()
    {
        var money = Money.RoundToPaisa(10.555m, MidpointRounding.AwayFromZero);
        Assert.Equal(10.56m, money.Amount);

        var moneyDown = Money.RoundToPaisa(10.554m);
        Assert.Equal(10.55m, moneyDown.Amount);
    }

    [Fact]
    public void RoundToPaisa_WithNegativeAmount_ThrowsArgumentOutOfRangeException()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Money.RoundToPaisa(-1.234m));
    }

    [Fact]
    public void Addition_AddsAmountsCorrectly()
    {
        var m1 = Money.FromNpr(50.25m);
        var m2 = Money.FromNpr(25.50m);

        var result = m1 + m2;

        Assert.Equal(75.75m, result.Amount);
    }

    [Fact]
    public void Subtraction_SubtractsAmountsCorrectly()
    {
        var m1 = Money.FromNpr(50.25m);
        var m2 = Money.FromNpr(25.25m);

        var result = m1 - m2;

        Assert.Equal(25.00m, result.Amount);
    }

    [Fact]
    public void Subtraction_ResultingInNegative_ThrowsInvalidOperationException()
    {
        var m1 = Money.FromNpr(10.00m);
        var m2 = Money.FromNpr(20.00m);

        Assert.Throws<InvalidOperationException>(() => m1 - m2);
    }

    [Fact]
    public void Multiplication_MultipliesByWholeNumberQuantity()
    {
        var unitPrice = Money.FromNpr(12.50m);

        var total = unitPrice * 4;

        Assert.Equal(50.00m, total.Amount);
        Assert.Equal(total, 4 * unitPrice);
    }

    [Fact]
    public void Multiplication_WithNegativeQuantity_ThrowsArgumentOutOfRangeException()
    {
        var unitPrice = Money.FromNpr(12.50m);

        Assert.Throws<ArgumentOutOfRangeException>(() => unitPrice * -1);
    }

    [Fact]
    public void Comparisons_WorkCorrectly()
    {
        var m1 = Money.FromNpr(10.00m);
        var m2 = Money.FromNpr(20.00m);
        var m3 = Money.FromNpr(20.00m);

        Assert.True(m1 < m2);
        Assert.True(m1 <= m2);
        Assert.True(m2 > m1);
        Assert.True(m2 >= m1);
        Assert.True(m2 >= m3);
        Assert.True(m2 <= m3);
        Assert.Equal(m2, m3);
        Assert.NotEqual(m1, m2);
    }
}
