// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Two currencies sharing a numeric code in the exchange-rate registry. CNY and CNH both carry ISO 4217's 156
/// (<c>asia.cpp:44</c>, <c>:248</c>), and QuantLib keys a pair by the numeric codes
/// (<c>exchangeratemanager.cpp:89-98</c>), so a USD/CNY rate and a USD/CNH rate share one entry list: with both held, a
/// lookup of USD to CNY returns the USD/CNH rate added last (100 USD exchange into 715 CNH), and a USD/CNY rate answers
/// a lookup of USD to CNH, shadowing a chain through EUR. nanoql keys a pair by its two currencies, so each lookup
/// finds its own pair's rate, or chains.
/// </summary>
public class SharedNumericCodeTests
{
    [Fact]
    public void CnyAndCnhAreLookedUpApart()
    {
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.October, 2026) };
        Currency usd = new USDCurrency(), cny = new CNYCurrency(), cnh = new CNHCurrency();
        ExchangeRates rates = ExchangeRates.Default
            .Add(new ExchangeRate(usd, cny, 7.10))
            .Add(new ExchangeRate(usd, cnh, 7.15));

        // QuantLib: USD to CNY and CNY to USD both return the USD/CNH rate, 7.15, which exchanges 100 USD into 715 CNH
        // and refuses an amount in CNY ("exchange rate not applicable")
        ExchangeRate usdCny = rates.Lookup(usd, cny, context);
        Assert.Equal(cny, usdCny.Target);
        Assert.Equal(7.10, usdCny.Rate);
        Assert.Equal(new Money(710.0, cny), usdCny.Exchange(new Money(100.0, usd)));
        Assert.Equal(new Money(71.0 / 7.10, usd), rates.Lookup(cny, usd, context).Exchange(new Money(71.0, cny)));

        // QuantLib's own: USD to CNH, 7.15
        Assert.Equal(7.15, rates.Lookup(usd, cnh, context).Rate);

        // a chain forbids the currencies it has passed, not their codes, so CNY reaches HKD through USD and CNH;
        // QuantLib returns the CNH/HKD rate, 1.09, for CNY to HKD
        Currency hkd = new HKDCurrency();
        ExchangeRate cnyHkd = rates.Add(new ExchangeRate(cnh, hkd, 1.09)).Lookup(cny, hkd, context);
        Assert.Equal(cny, cnyHkd.Source);
        Assert.Equal(hkd, cnyHkd.Target);
        Assert.Equal(7.15 / 7.10 * 1.09, cnyHkd.Rate, 1e-15);
    }

    [Fact]
    public void AUsdCnyRateNoLongerShadowsAChainForCnh()
    {
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.October, 2026) };
        Currency usd = new USDCurrency(), eur = new EURCurrency(), cny = new CNYCurrency(), cnh = new CNHCurrency();
        ExchangeRates rates = ExchangeRates.Default
            .Add(new ExchangeRate(eur, cnh, 7.75))
            .Add(new ExchangeRate(eur, usd, 1.0856))
            .Add(new ExchangeRate(usd, cny, 7.10));

        // QuantLib: USD to CNH and CNH to USD return the USD/CNY rate, 7.1
        ExchangeRate usdCnh = rates.Lookup(usd, cnh, context);
        Assert.Equal(ExchangeRateType.Derived, usdCnh.Type);
        Assert.Equal(usd, usdCnh.Source);
        Assert.Equal(cnh, usdCnh.Target);
        Assert.Equal(7.75 / 1.0856, usdCnh.Rate);

        // QuantLib's own, the chain's legs
        Assert.Equal(7.10, rates.Lookup(usd, cny, context).Rate);
        Assert.Equal(7.75, rates.Lookup(eur, cnh, context, type: ExchangeRateType.Direct).Rate);
    }
}
