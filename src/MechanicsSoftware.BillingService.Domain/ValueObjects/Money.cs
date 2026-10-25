using MechanicsSoftware.BillingService.Domain.Exceptions;

namespace MechanicsSoftware.BillingService.Domain.ValueObjects;

public sealed class Money : ValueObject
{
    public const string Currency = "BRL";

    public int Cents { get; }

    public Money(int cents)
    {
        if (cents < 0)
            throw new DomainException("Money amount cannot be negative.");
        Cents = cents;
    }

    public static Money Zero => new(0);

    public Money Add(Money other) => new(Cents + other.Cents);

    public Money Multiply(int factor)
    {
        if (factor < 0)
            throw new DomainException("Multiply factor cannot be negative.");
        return new(Cents * factor);
    }

    public string ToFormatted()
    {
        var reais = Cents / 100;
        var centavos = Cents % 100;
        return $"R$ {reais},{centavos:D2}";
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Cents;
    }

    public override string ToString() => ToFormatted();
}
