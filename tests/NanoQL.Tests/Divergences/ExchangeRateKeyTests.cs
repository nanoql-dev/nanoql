// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The exchange-rate registry's key for currencies coded 1,000 or more. QuantLib keys a pair by
/// <c>min(code) × 1000 + max(code)</c> and finds a currency's pairs by the key's thousands and units
/// (<c>exchangeratemanager.cpp:89-98</c>), so a currency coded 1,000 or more (QuantLib's crypto currencies, 10000 to
/// 10007, or any currency of one's own) matches none of its pairs: with BTC/EUR and EUR/USD held, a lookup from BTC to
/// USD throws "no conversion available" while the reverse chains, and no chain passes through BTC. nanoql keys a pair
/// by its two currencies, in QuantLib's order, so both lookups chain, and chains pass through it.
/// </summary>
public class ExchangeRateKeyTests
{
    private static readonly Currency Btc = new("Bitcoin", "BTC", 10000, "BTC", "", 100000000, new Rounding());
    private static readonly Currency Eth = new("Ethereum", "ETH", 10001, "ETH", "", 100000000, new Rounding());

    [Fact]
    public void APairWithACodeOf10000IsLookedUpBothWays()
    {
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.October, 2026) };
        Currency eur = new EURCurrency(), usd = new USDCurrency();
        ExchangeRates rates = ExchangeRates.Default
            .Add(new ExchangeRate(Btc, eur, 61234.5))
            .Add(new ExchangeRate(eur, usd, 1.0856));

        // QuantLib: "no conversion available from BTC to USD for October 15th, 2026"
        ExchangeRate btcUsd = rates.Lookup(Btc, usd, context);
        Assert.Equal(Btc, btcUsd.Source);
        Assert.Equal(usd, btcUsd.Target);
        Assert.Equal(ExchangeRateType.Derived, btcUsd.Type);
        Assert.Equal(61234.5 * 1.0856, btcUsd.Rate);

        // QuantLib's own: USD to BTC chains, at 1 / (61234.5 × 1.0856), 1.504298385214509e-05
        ExchangeRate usdBtc = rates.Lookup(usd, Btc, context);
        Assert.Equal(1.504298385214509e-05, usdBtc.Rate);
        Assert.Equal(1.0, usdBtc.Rate * btcUsd.Rate, 1e-15);
    }

    [Fact]
    public void AChainPassesThroughACodeOf10000()
    {
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.October, 2026) };
        Currency usd = new USDCurrency();
        ExchangeRates rates = ExchangeRates.Default
            .Add(new ExchangeRate(usd, Btc, 1.0 / 66500.0))
            .Add(new ExchangeRate(Btc, Eth, 25.4));

        // QuantLib: "no conversion available from USD to ETH" and "from ETH to USD", each leg's direct lookup its own
        ExchangeRate usdEth = rates.Lookup(usd, Eth, context);
        Assert.Equal(1.0 / 66500.0 * 25.4, usdEth.Rate);
        Money ethInUsd = rates.Lookup(Eth, usd, context).Exchange(new Money(10.0, Eth));
        Assert.Equal(usd, ethInUsd.Currency);
        Assert.Equal(10.0 / 25.4 * 66500.0, ethInUsd.Value, 1e-9);

        // each leg as QuantLib's direct lookup returns it
        Assert.Equal(1.0 / 66500.0, rates.Lookup(usd, Btc, context, type: ExchangeRateType.Direct).Rate);
        Assert.Equal(25.4, rates.Lookup(Btc, Eth, context, type: ExchangeRateType.Direct).Rate);
    }
}
