// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="Money"/>'s operators keep QuantLib's <c>NoConversion</c> (U7, the S-rx plan's SRXR1): they see no
/// context, so a currency mismatch is refused by every operator but <c>==</c> and <c>!=</c>, which compare amounts in
/// different currencies as unequal; within a currency they are QuantLib's arithmetic; and the text is QuantLib's
/// stream.
/// </summary>
public class MoneyOperatorTests
{
    private static readonly Currency Eur = new EURCurrency();
    private static readonly Currency Usd = new USDCurrency();

    [Fact]
    public void AMismatchIsRefusedByEveryOperatorButEquality()
    {
        Money eur = 1.0 * Eur, usd = 2.0 * Usd;
        Action[] refused =
        [
            () => _ = eur + usd,
            () => _ = eur - usd,
            () => _ = eur / usd,
            () => _ = eur < usd,
            () => _ = eur <= usd,
            () => _ = eur > usd,
            () => _ = eur >= usd,
        ];
        foreach (Action a in refused)
        {
            NanoQLException e = Assert.Throws<NanoQLException>(a);
            Assert.Equal("currency mismatch and no conversion specified", e.Message);
        }
    }

    [Fact]
    public void EqualityComparesAMismatchAsUnequal()
    {
        Money eur = 1.0 * Eur, usd = 1.0 * Usd;

        // QuantLib's == and != throw "currency mismatch and no conversion specified" under NoConversion
        Assert.False(eur == usd);
        Assert.True(eur != usd);
        Assert.False(eur.Equals(usd));
        Assert.False(new Money(1.0, null) == eur);

        // within a currency, IEEE's ==; Equals by double.Equals, so a NaN amount equals itself
        Assert.True(new Money(2.5, Eur) == new Money(2.5, new EURCurrency()));
        Money nan = new(double.NaN, Eur), nan2 = new(double.NaN, new EURCurrency());
        Assert.False(nan == nan2);
        Assert.True(nan.Equals(nan2));
        Assert.Equal(nan.GetHashCode(), new Money(double.NaN, new EURCurrency()).GetHashCode());
        Assert.True(default(Money) == new Money(0.0, null));
    }

    [Fact]
    public void ArithmeticWithinACurrencyIsQuantLibs()
    {
        Money a = 50000.0 * Eur, b = Eur * 20000.0;
        Assert.Equal(new Money(70000.0, Eur), a + b);
        Assert.Equal(new Money(30000.0, Eur), a - b);
        Assert.Equal(new Money(-50000.0, Eur), -a);
        Assert.Equal(a, +a);
        Assert.Equal(new Money(150000.0, Eur), a * 3.0);
        Assert.Equal(new Money(150000.0, Eur), 3.0 * a);
        Assert.Equal(new Money(12500.0, Eur), a / 4.0);
        Assert.Equal(2.5, a / b);
        Money a2 = new(a.Value, Eur);
        Assert.True(b < a && b <= a && a > b && a >= b && a <= a2 && a >= a2 && !(a < a2) && !(a > a2));

        // QuantLib's other constructor, currency first; and equality through object
        Assert.Equal(a2, new Money(Eur, a.Value));
        Assert.True(a.Equals((object)a2));
        Assert.False(a.Equals((object)b));
        Assert.False(a.Equals((object)a.Value));
    }

    [Fact]
    public void TheTextIsQuantLibsStream()
    {
        // the rounded value in C++'s default stream format, six significant digits, then the code
        Assert.Equal("1.23457e+06 EUR", new Money(1234567.891, Eur).ToString());
        Assert.Equal("0.12 EUR", new Money(0.123456789, Eur).ToString());
        Assert.Equal("100 EUR", new Money(100.0, Eur).ToString());
        Assert.Equal("0 EUR", new Money(1e-7, Eur).ToString());
        Assert.Equal("-0 EUR", new Money(-0.004, Eur).ToString());
        Assert.Equal("1e+20 JPY", new Money(1e20, new JPYCurrency()).ToString());
        Assert.Equal("nan EUR", new Money(double.NaN, Eur).ToString());
        Assert.Equal("inf EUR", new Money(double.PositiveInfinity, Eur).ToString());
        Assert.Equal("-inf EUR", new Money(double.NegativeInfinity, Eur).ToString());

        // QuantLib's empty currency has no rounding: "no currency data provided"
        NanoQLException e = Assert.Throws<NanoQLException>(() => new Money(1.0, null).ToString());
        Assert.Equal("no currency data provided", e.Message);
    }
}
