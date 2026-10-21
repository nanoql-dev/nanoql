// SPDX-FileCopyrightText: Copyright (C) 2004 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: test-suite/money.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Currencies;
using NanoQL.Time;

namespace NanoQL.Tests.TestSuite;

// QuantLib's global Money::Settings and ExchangeRateManager are the context's MoneySettings and ExchangeRates; a case
// that converts sets them on its context, and the operators QuantLib's case writes become the methods that take it.
[Trait("Oracle", "QuantLib")]
public class MoneyTests
{
    private static readonly Currency EUR = new EURCurrency();
    private static readonly Currency GBP = new GBPCurrency();
    private static readonly Currency USD = new USDCurrency();
    private static readonly ExchangeRate EurUsd = new(EUR, USD, 1.2042);
    private static readonly ExchangeRate EurGbp = new(EUR, GBP, 0.6612);

    [Fact]
    public void TestNone()
    {
        // Testing money arithmetic without conversions...

        Money m1 = 50000.0 * EUR;
        Money m2 = 100000.0 * EUR;
        Money m3 = 500000.0 * EUR;

        // QuantLib's NoConversion, the operators' own behaviour
        Money calculated = (m1 * 3.0) + (2.5 * m2) - (m3 / 5.0) + (m1 * (m2 / m3));
        double x = (m1.Value * 3.0)
            + (2.5 * m2.Value)
            - (m3.Value / 5.0)
            + (m1.Value * (m2.Value / m3.Value));
        var expected = new Money(x, EUR);

        Assert.True(IsSameCurrencyAndValuesAreClose(calculated, expected), Message(expected, calculated));
    }

    [Fact]
    public void TestBaseCurrency()
    {
        // Testing money arithmetic with conversion to base currency...

        Money gbp = 50000.0 * GBP;
        Money eur = 100000.0 * EUR;
        Money usd = 500000.0 * USD;

        EvaluationContext context = Context() with
        {
            ExchangeRates = ExchangeRates.Default.Add(EurUsd).Add(EurGbp),
            MoneySettings = new MoneySettings
            {
                ConversionType = MoneyConversionType.BaseCurrencyConversion,
                BaseCurrency = EUR,
            },
        };
        double GbpToEur(double v) => v / EurGbp.Rate;
        double UsdToEur(double v) => v / EurUsd.Rate;

        Money calculated = (gbp * 3.0).Add(2.5 * eur, context).Subtract(usd / 5.0, context)
            .Add(gbp * eur.Divide(usd, context), context);

        double x = (GbpToEur(gbp.Value) * 3.0)
            + (2.5 * eur.Value)
            - (UsdToEur(usd.Value) / 5.0)
            + (GbpToEur(gbp.Value) * eur.Value / UsdToEur(usd.Value));
        var expected = new Money(x, EUR);

        Assert.True(IsSameCurrencyAndValuesAreClose(calculated, expected), Message(expected, calculated));
    }

    [Fact]
    public void TestAutomated()
    {
        // Testing money arithmetic with automated conversion...

        Money gbp = 50000.0 * GBP;
        Money eur = 100000.0 * EUR;
        Money usd = 500000.0 * USD;

        EvaluationContext context = Context() with
        {
            ExchangeRates = ExchangeRates.Default.Add(EurUsd).Add(EurGbp),
            MoneySettings = new MoneySettings { ConversionType = MoneyConversionType.AutomatedConversion },
        };
        double EurToGbp(double v) => v * EurGbp.Rate;
        double UsdToEur(double v) => v / EurUsd.Rate;
        double UsdToGbp(double v) => v * EurGbp.Rate / EurUsd.Rate;

        Money calculated = (gbp * 3.0).Add(2.5 * eur, context).Subtract(usd / 5.0, context)
            .Add(gbp * eur.Divide(usd, context), context);

        double x = (gbp.Value * 3.0)
            + (2.5 * EurToGbp(eur.Value))
            - (UsdToGbp(usd.Value) / 5.0)
            + (gbp.Value * eur.Value / UsdToEur(usd.Value));

        var expected = new Money(x, GBP);

        Assert.True(IsSameCurrencyAndValuesAreClose(calculated, expected), Message(expected, calculated));
    }

    [Fact]
    public void TestComparisons()
    {
        // Testing money comparisons...

        foreach (MoneyConversionType conversionType in new[]
        {
            MoneyConversionType.AutomatedConversion,
            MoneyConversionType.NoConversion,
            MoneyConversionType.BaseCurrencyConversion,
        })
        {
            EvaluationContext context = Context() with
            {
                ExchangeRates = ExchangeRates.Default.Add(EurUsd).Add(EurGbp),
                MoneySettings = new MoneySettings
                {
                    ConversionType = conversionType,
                    BaseCurrency = conversionType == MoneyConversionType.BaseCurrencyConversion ? EUR : null,
                },
            };

            // equality
            Assert.True(new Money(123.45, EUR).Equals(new Money(123.45, EUR), context));
            if (conversionType != MoneyConversionType.NoConversion)
            {
                Assert.True(new Money(1, EUR).Equals(new Money(EurUsd.Rate, USD), context));
            }

            // unequal
            Assert.False(new Money(1, EUR).Equals(new Money(2, EUR), context));
            if (conversionType != MoneyConversionType.NoConversion)
            {
                Assert.False(new Money(1, EUR).Equals(new Money(100, USD), context));
            }

            // less than
            Assert.True(new Money(1, EUR).LessThan(new Money(2, EUR), context));
            if (conversionType != MoneyConversionType.NoConversion)
            {
                Assert.True(new Money(1, EUR).LessThan(new Money(100, USD), context));
            }

            // less or equal than
            Assert.True(new Money(1, EUR).LessThanOrEqual(new Money(2, EUR), context));
            Assert.True(new Money(2, EUR).LessThanOrEqual(new Money(2, EUR), context));
            if (conversionType != MoneyConversionType.NoConversion)
            {
                Assert.True(new Money(1, EUR).LessThanOrEqual(new Money(100, USD), context));
            }

            // greater than
            Assert.True(new Money(2, EUR).GreaterThan(new Money(1, EUR), context));
            if (conversionType != MoneyConversionType.NoConversion)
            {
                Assert.True(new Money(100, EUR).GreaterThan(new Money(1, USD), context));
            }

            // less or equal than
            Assert.True(new Money(2, EUR).GreaterThanOrEqual(new Money(1, EUR), context));
            Assert.True(new Money(2, EUR).GreaterThanOrEqual(new Money(2, EUR), context));
            if (conversionType != MoneyConversionType.NoConversion)
            {
                Assert.True(new Money(100, EUR).GreaterThanOrEqual(new Money(1, USD), context));
            }

            // close
            Assert.True(MoneyFunctions.Close(new Money(1, EUR), new Money(1, EUR), context));
            Assert.True(MoneyFunctions.Close(new Money(1 + 1e-15, EUR), new Money(1, EUR), context));
            if (conversionType != MoneyConversionType.NoConversion)
            {
                Assert.True(MoneyFunctions.Close(new Money(1, EUR), new Money(EurUsd.Rate, USD), context));
                Assert.True(MoneyFunctions.Close(new Money(1 + 1e-15, EUR), new Money(EurUsd.Rate, USD), context));
            }

            // close enough
            Assert.True(MoneyFunctions.CloseEnough(new Money(1, EUR), new Money(1, EUR), context));
            Assert.True(MoneyFunctions.CloseEnough(new Money(1 + 1e-15, EUR), new Money(1, EUR), context));
            if (conversionType != MoneyConversionType.NoConversion)
            {
                Assert.True(MoneyFunctions.CloseEnough(new Money(1, EUR), new Money(EurUsd.Rate, USD), context));
                Assert.True(MoneyFunctions.CloseEnough(new Money(1 + 1e-15, EUR), new Money(EurUsd.Rate, USD), context));
            }
        }
    }

    private static EvaluationContext Context() => new() { EvaluationDate = new Date(16, Month.September, 2015) };

    private static bool IsSameCurrencyAndValuesAreClose(Money lhs, Money rhs) =>
        lhs.Currency == rhs.Currency && Math.Abs(lhs.Value - rhs.Value) < 0.01;

    private static string Message(Money expected, Money calculated) =>
        $"Wrong result: \n    expected:   {expected}\n    calculated: {calculated}";
}
