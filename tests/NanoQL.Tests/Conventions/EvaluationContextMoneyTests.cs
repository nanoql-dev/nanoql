// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The context's two fields for QuantLib's <c>ExchangeRateManager</c> and <c>Money::Settings</c>: their defaults are
/// QuantLib's, the known rates and no conversion with no base currency; neither takes null; and the record's equality
/// compares the registry by content and the settings by value.
/// </summary>
public class EvaluationContextMoneyTests
{
    [Fact]
    public void TheDefaultsAreQuantLibsAndEqualityReadsTheRegistrysContent()
    {
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.October, 2026) };
        Assert.Same(ExchangeRates.Default, context.ExchangeRates);
        Assert.Equal(MoneyConversionType.NoConversion, context.MoneySettings.ConversionType);
        Assert.Null(context.MoneySettings.BaseCurrency);
        Assert.Throws<ArgumentNullException>(() => context with { ExchangeRates = null! });
        Assert.Throws<ArgumentNullException>(() => context with { MoneySettings = null! });
        Assert.Throws<ArgumentOutOfRangeException>(() => new MoneySettings { ConversionType = (MoneyConversionType)3 });

        EvaluationContext a = context with
        {
            ExchangeRates = ExchangeRates.Default.Add(new ExchangeRate(new EURCurrency(), new USDCurrency(), 1.10)),
            MoneySettings = new MoneySettings { ConversionType = MoneyConversionType.BaseCurrencyConversion, BaseCurrency = new EURCurrency() },
        };
        EvaluationContext b = context with
        {
            ExchangeRates = ExchangeRates.Default.Add(new ExchangeRate(new EURCurrency(), new USDCurrency(), 1.10)),
            MoneySettings = new MoneySettings { ConversionType = MoneyConversionType.BaseCurrencyConversion, BaseCurrency = new EURCurrency() },
        };
        Assert.NotSame(a.ExchangeRates, b.ExchangeRates);
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(context, a);
        Assert.NotEqual(a, b with { ExchangeRates = ExchangeRates.Default.Add(new ExchangeRate(new EURCurrency(), new USDCurrency(), 1.11)) });
        Assert.NotEqual(a, b with { MoneySettings = b.MoneySettings with { BaseCurrency = new USDCurrency() } });
    }
}
