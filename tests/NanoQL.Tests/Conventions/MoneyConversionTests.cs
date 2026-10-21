// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="Money"/>'s conversion modes are methods that take the context (U7, the S-rx plan's SRXR1): each reads
/// <see cref="EvaluationContext.MoneySettings"/> and converts through <see cref="EvaluationContext.ExchangeRates"/> at
/// the context's evaluation date, as QuantLib's operators read its globals; <see cref="Money.GreaterThan"/> converts
/// the operand QuantLib's <c>&gt;</c> converts; under no conversion each method answers as its operator does.
/// </summary>
public class MoneyConversionTests
{
    private static readonly Currency Eur = new EURCurrency();
    private static readonly Currency Usd = new USDCurrency();
    private static readonly Currency Gbp = new GBPCurrency();

    [Fact]
    public void EachModeConvertsThroughTheContext()
    {
        var today = new Date(15, Month.October, 2026);
        ExchangeRates rates = ExchangeRates.Default
            .Add(new ExchangeRate(Eur, Usd, 1.10), today - 10, today)
            .Add(new ExchangeRate(Eur, Usd, 1.20), today + 1, Date.MaxDate);
        var context = new EvaluationContext { EvaluationDate = today, ExchangeRates = rates };
        Money eur = 100.0 * Eur, usd = 110.0 * Usd;

        // automated: the second operand into the first's currency, at the evaluation date's rate, rounded
        EvaluationContext automated = context with { MoneySettings = new MoneySettings { ConversionType = MoneyConversionType.AutomatedConversion } };
        Assert.Equal(new Money(200.0, Eur), eur.Add(usd, automated));
        Assert.Equal(new Money(0.0, Eur), eur.Subtract(usd, automated));
        Assert.Equal(1.0, eur.Divide(usd, automated));
        Assert.True(eur.Equals(usd, automated));
        Assert.Equal(new Money(220.0, Usd), usd.Add(eur, automated));

        // the next day's rate, 1.20: 110 USD is 91.67 EUR, rounded to the cent
        EvaluationContext tomorrow = automated with { EvaluationDate = today + 1 };
        Money sum = eur.Add(usd, tomorrow);
        Assert.Equal(Eur, sum.Currency);
        Assert.Equal(100.0 + 91.67, sum.Value);
        Assert.True(usd.LessThan(eur, tomorrow));

        // base currency: both operands into it, the result in it
        EvaluationContext toGbp = context with
        {
            ExchangeRates = rates.Add(new ExchangeRate(Eur, Gbp, 0.80)),
            MoneySettings = new MoneySettings { ConversionType = MoneyConversionType.BaseCurrencyConversion, BaseCurrency = Gbp },
        };
        Money inGbp = eur.Add(usd, toGbp);
        Assert.Equal(Gbp, inGbp.Currency);
        Assert.Equal(160.0, inGbp.Value, 1e-12);
        Assert.Equal(new Money(200.0, Eur), eur.Add(eur, toGbp));

        // no base currency set
        NanoQLException noBase = Assert.Throws<NanoQLException>(() => eur.Add(usd, context with
        {
            MoneySettings = new MoneySettings { ConversionType = MoneyConversionType.BaseCurrencyConversion },
        }));
        Assert.Equal("no base currency set", noBase.Message);

        // under no conversion each method answers as its operator does, Equals as ==
        Assert.False(eur.Equals(usd, context));
        Assert.Equal(eur == usd, eur.Equals(usd, context));
        Assert.Equal(eur + eur, eur.Add(eur, context));
        Assert.Equal(eur - (2.0 * eur), eur.Subtract(2.0 * eur, context));
        Assert.Equal(eur / (4.0 * eur), eur.Divide(4.0 * eur, context));
        Assert.Equal(eur < (2.0 * eur), eur.LessThan(2.0 * eur, context));
        Assert.Equal(eur > (2.0 * eur), eur.GreaterThan(2.0 * eur, context));
        Assert.Throws<NanoQLException>(() => eur.Add(usd, context));
        Assert.Throws<NanoQLException>(() => eur.LessThanOrEqual(usd, context));
    }

    [Fact]
    public void GreaterThanConvertsTheOtherOperand()
    {
        var context = new EvaluationContext
        {
            EvaluationDate = new Date(15, Month.October, 2026),
            ExchangeRates = ExchangeRates.Default.Add(new ExchangeRate(Eur, Gbp, 0.6612)),
            MoneySettings = new MoneySettings { ConversionType = MoneyConversionType.AutomatedConversion },
        };

        // GBP has no rounding and EUR rounds to the cent: m1 into EUR is 1.51, m2 into GBP is exactly 1.0
        Money m1 = (1.0 + 1e-15) * Gbp, m2 = (1.0 / 0.6612) * Eur;

        // QuantLib's m1 >= m2 is m2 <= m1, which converts m1 into EUR: 1.5124... <= 1.51, false; converting m2, as
        // !(m1 < m2) would, gives true
        Assert.False(m1.GreaterThanOrEqual(m2, context));
        Assert.Equal(m2.LessThanOrEqual(m1, context), m1.GreaterThanOrEqual(m2, context));
        Assert.True(!m1.LessThan(m2, context));

        // m1 > m2 is m2 < m1: 1.5124... < 1.51, false; m1 <= m2 converts m2: 1.000000000000001 <= 1.0, false; and
        // m2 > m1 is m1 < m2, converting m2: false; neither is greater, nor are they equal, QuantLib's own answers
        Assert.False(m1.GreaterThan(m2, context));
        Assert.False(m1.LessThanOrEqual(m2, context));
        Assert.False(m2.GreaterThan(m1, context));
        Assert.False(m1.Equals(m2, context));
    }

    [Fact]
    public void CloseReadsTheContext()
    {
        var context = new EvaluationContext
        {
            EvaluationDate = new Date(15, Month.October, 2026),
            ExchangeRates = ExchangeRates.Default.Add(new ExchangeRate(Eur, Usd, 1.2042)),
        };
        EvaluationContext automated = context with { MoneySettings = new MoneySettings { ConversionType = MoneyConversionType.AutomatedConversion } };

        Assert.True(MoneyFunctions.Close(new Money(1.0, Eur), new Money(1.2042, Usd), automated));
        Assert.True(MoneyFunctions.CloseEnough(new Money(1.0, Eur), new Money(1.2042, Usd), automated));
        Assert.True(MoneyFunctions.Close(new Money(1.0, Eur), new Money(1.21, Usd), automated)); // 1.0048 EUR, rounded
        Assert.False(MoneyFunctions.Close(new Money(1.0, Eur), new Money(1.25, Usd), automated)); // 1.04 EUR
        Assert.False(MoneyFunctions.Close(new Money(1.0, Eur), new Money(1.0 + 1e-12, Eur), context));
        Assert.True(MoneyFunctions.Close(new Money(1.0, Eur), new Money(1.0 + 1e-15, Eur), context, 42));
        Assert.False(MoneyFunctions.Close(new Money(1.0, Eur), new Money(1.0 + 1e-15, Eur), context, 1));

        // under no conversion a mismatch is refused, as QuantLib's close refuses it
        NanoQLException e = Assert.Throws<NanoQLException>(
            () => MoneyFunctions.CloseEnough(new Money(1.0, Eur), new Money(1.2042, Usd), context));
        Assert.Equal("currency mismatch and no conversion specified", e.Message);
    }
}
