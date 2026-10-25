using FluentAssertions;
using MechanicsSoftware.BillingService.Domain.Exceptions;
using MechanicsSoftware.BillingService.Domain.ValueObjects;

namespace MechanicsSoftware.BillingService.UnitTests.Domain;

public class MoneyTests
{
    [Fact]
    public void Constructor_NegativeAmount_ThrowsDomainException()
    {
        var act = () => new Money(-1);

        act.Should().Throw<DomainException>().WithMessage("*negative*");
    }

    [Fact]
    public void Add_TwoAmounts_ReturnsSum()
    {
        new Money(1050).Add(new Money(250)).Should().Be(new Money(1300));
    }

    [Fact]
    public void Multiply_PositiveFactor_ReturnsProduct()
    {
        new Money(4500).Multiply(3).Should().Be(new Money(13500));
    }

    [Fact]
    public void Multiply_NegativeFactor_ThrowsDomainException()
    {
        var act = () => new Money(100).Multiply(-1);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Zero_ReturnsZeroCents()
    {
        Money.Zero.Cents.Should().Be(0);
    }

    [Theory]
    [InlineData(0, "R$ 0,00")]
    [InlineData(5, "R$ 0,05")]
    [InlineData(123456, "R$ 1234,56")]
    public void ToString_AnyAmount_ReturnsBrazilianFormat(int cents, string expected)
    {
        new Money(cents).ToString().Should().Be(expected);
    }

    [Fact]
    public void Equals_SameCents_ReturnsTrueAndSameHashCode()
    {
        var first = new Money(700);
        var second = new Money(700);

        first.Equals(second).Should().BeTrue();
        first.GetHashCode().Should().Be(second.GetHashCode());
    }

    [Fact]
    public void Equals_DifferentTypeOrNull_ReturnsFalse()
    {
        var money = new Money(700);

        money.Equals(BudgetStatus.CreatePending()).Should().BeFalse();
        money.Equals(null).Should().BeFalse();
    }
}
