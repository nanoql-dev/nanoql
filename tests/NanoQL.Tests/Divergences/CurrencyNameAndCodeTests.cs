// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Two currencies with one name. QuantLib's <c>operator==</c> compares names alone (<c>currency.hpp:179-182</c>), and
/// it names both BGL, the lev of 1962 (numeric code 100), and BGN, the lev of 1999 (975), "Bulgarian lev"
/// (<c>europe.cpp:35</c>, <c>:389</c>): QuantLib takes them for one currency, so 1,000 BGL plus 1 BGN is 1,001 BGL, a
/// EUR/BGN rate exchanges 1,000 BGL into 511.29 EUR, a lookup from BGL to BGN is 1.0 whatever the registry holds, and a
/// forward from BGL to BGN is refused as one currency. nanoql compares currencies by name and code, so they are two;
/// each expected value is QuantLib's own with BGL renamed apart ("Bulgarian lev (1962)"), its data otherwise
/// QuantLib's. The exchange-rate registry tells currencies apart by QuantLib's numeric code and chains as QuantLib's
/// <c>chain</c> does, so a currency equal to another with another numeric code is chained as QuantLib chains it.
/// </summary>
public class CurrencyNameAndCodeTests
{
    private static readonly Date Today = new(15, Month.October, 2026);

    [Fact]
    public void BglAndBgnAreTwoCurrencies()
    {
        var context = new EvaluationContext { EvaluationDate = Today };
        Currency bgl = new BGLCurrency(), bgn = new BGNCurrency();
        Assert.Equal(bgl.Name, bgn.Name);

        // QuantLib: equal
        Assert.False(bgl == bgn);
        Assert.NotEqual(bgl, bgn);

        // QuantLib: 1001 BGL
        NanoQLException mismatch = Assert.Throws<NanoQLException>(() => new Money(1000.0, bgl) + new Money(1.0, bgn));
        Assert.Equal("currency mismatch and no conversion specified", mismatch.Message);

        // QuantLib: 511.29 EUR
        var eurBgn = new ExchangeRate(new EURCurrency(), bgn, 1.95583);
        ArgumentException notApplicable = Assert.Throws<ArgumentException>(() => eurBgn.Exchange(new Money(1000.0, bgl)));
        Assert.StartsWith("exchange rate not applicable", notApplicable.Message, StringComparison.Ordinal);

        // QuantLib: "source and target currencies must be different"
        var forward = new FxForward(1000.0, bgl, 1.0, bgn, Today + new Period(1, TimeUnit.Years), true);
        Assert.Equal(bgn, forward.TargetCurrency);

        // QuantLib: BGL→BGN at 1.0 whatever the registry holds, exchanging 1000 BGL into 1000 BGN
        ExchangeRate held = ExchangeRates.Default.Clear().Add(new ExchangeRate(bgn, bgl, 1000.0)).Lookup(bgl, bgn, context);
        Assert.Equal(bgn, held.Source);
        Assert.Equal(bgl, held.Target);
        Assert.Equal(1000.0, held.Rate);
        Assert.Equal(new Money(1.0, bgn), held.Exchange(new Money(1000.0, bgl)));

        ExchangeRates chains = ExchangeRates.Default.Clear()
            .Add(new ExchangeRate(bgl, new USDCurrency(), 0.0005))
            .Add(new ExchangeRate(new USDCurrency(), bgn, 1.8))
            .Add(new ExchangeRate(bgn, new JPYCurrency(), 85.0));
        ExchangeRate chained = chains.Lookup(bgl, bgn, context);
        Assert.Equal(bgl, chained.Source);
        Assert.Equal(bgn, chained.Target);
        Assert.Equal(ExchangeRateType.Derived, chained.Type);
        Assert.Equal(0.0009000000000000001, chained.Rate);
    }

    [Fact]
    public void TheRegistryTellsCurrenciesApartByTheirNumericCodesAsQuantLibs()
    {
        // A currency with USD's name and code but a numeric code no QuantLib currency has (996) is USD's equal, here as
        // in QuantLib, but the registry keys it apart, and a chain that has passed USD passes it, as QuantLib's
        // forbidden list of numeric codes lets it (exchangeratemanager.cpp:159, :170): QuantLib's own USD→JPY,
        // 0.92 × (1.1 × 150) = 151.8.
        var context = new EvaluationContext { EvaluationDate = Today };
        Currency usd = new USDCurrency(), eur = new EURCurrency(), jpy = new JPYCurrency();
        var usdTwin = new Currency("U.S. dollar", "USD", 996, "$", "", 100, new Rounding());
        Assert.True(usdTwin == usd);

        ExchangeRate rate = ExchangeRates.Default.Clear()
            .Add(new ExchangeRate(usd, eur, 0.92))
            .Add(new ExchangeRate(eur, usdTwin, 1.1))
            .Add(new ExchangeRate(usdTwin, jpy, 150.0))
            .Lookup(usd, jpy, context);
        Assert.Equal(840, rate.Source!.NumericCode);
        Assert.Equal(jpy, rate.Target);
        Assert.Equal(ExchangeRateType.Derived, rate.Type);
        Assert.Equal(151.8, rate.Rate);

        // A step whose two rates are over one pair by the currencies' equality, but not in the key, is chained as
        // QuantLib chains it: AUD to EUR through an EUR twin (997) and DEM, on QuantLib's known rates, QuantLib's own
        // 0.6135502574354622 each way.
        var eurTwin = new Currency("European Euro", "EUR", 997, "", "", 100, new Rounding());
        ExchangeRates twins = ExchangeRates.Default.Clear()
            .Add(new ExchangeRate(new AUDCurrency(), eurTwin, 0.6))
            .Add(new ExchangeRate(new DEMCurrency(), eurTwin, 0.5));
        ExchangeRate audEur = twins.Lookup(new AUDCurrency(), eur, context);
        Assert.Equal(new AUDCurrency(), audEur.Source);
        Assert.Equal(eur, audEur.Target);
        Assert.Equal(0.6135502574354622, audEur.Rate);
        Assert.Equal(1.6298583333333334, twins.Lookup(eur, new AUDCurrency(), context).Rate);
    }
}
