// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Chaining two exchange rates over one pair of currencies. QuantLib's <c>ExchangeRate::chain</c> matches the rates'
/// currencies in order (<c>exchangerate.cpp:57-73</c>), so EUR→USD at 1.1 with EUR→USD at 1.2 chains into USD→USD at
/// 1.0909, which exchanges 100 USD into 109.09 USD. nanoql refuses two rates over one pair with QuantLib's own
/// "exchange rates not chainable" (<c>:74</c>); rates over different pairs chain as QuantLib's.
/// </summary>
public class ExchangeRateChainRefusalTests
{
    [Fact]
    public void TwoRatesOverOnePairAreRefused()
    {
        Currency eur = new EURCurrency(), usd = new USDCurrency(), gbp = new GBPCurrency();
        var r1 = new ExchangeRate(eur, usd, 1.1);

        // QuantLib: USD→USD at 1.0909090909090908, 100 USD exchanged into 109.09090909090908 USD
        ArgumentException same = Assert.Throws<ArgumentException>(() => ExchangeRate.Chain(r1, new ExchangeRate(eur, usd, 1.2)));
        Assert.Equal("r2", same.ParamName);
        Assert.StartsWith("exchange rates not chainable", same.Message);

        // the pair crossed: QuantLib's USD→USD at 1 / (1.1 × (1 / 1.2))
        Assert.Throws<ArgumentException>(() => ExchangeRate.Chain(r1, new ExchangeRate(usd, eur, 1.0 / 1.2)));

        // QuantLib's own: two pairs sharing EUR chain into USD→GBP
        ExchangeRate usdGbp = ExchangeRate.Chain(r1, new ExchangeRate(eur, gbp, 0.66));
        Assert.Equal(usd, usdGbp.Source);
        Assert.Equal(gbp, usdGbp.Target);
        Assert.Equal(0.66 / 1.1, usdGbp.Rate);

        // QuantLib's empty currency, null: two rates from it to USD are one pair too (QuantLib: USD→USD at
        // 1.0909090909090908), and rates from it to two currencies chain as QuantLib's, USD→EUR at 1.8181818181818181
        Assert.Throws<ArgumentException>(
            () => ExchangeRate.Chain(new ExchangeRate(null, usd, 1.1), new ExchangeRate(null, usd, 1.2)));
        ExchangeRate usdEur = ExchangeRate.Chain(new ExchangeRate(null, usd, 1.1), new ExchangeRate(null, eur, 2.0));
        Assert.Equal(usd, usdEur.Source);
        Assert.Equal(eur, usdEur.Target);
        Assert.Equal(1.8181818181818181, usdEur.Rate);

        // a currency with USD's name and code but another numeric code (996, no QuantLib currency's) is USD's equal, so
        // its rate to EUR is over USD's pair (QuantLib: EUR→EUR at 0.9782608695652174)
        var twin = new Currency("U.S. dollar", "USD", 996, "$", "", 100, new Rounding());
        Assert.Throws<ArgumentException>(() => ExchangeRate.Chain(new ExchangeRate(usd, eur, 0.92), new ExchangeRate(twin, eur, 0.9)));
    }
}
