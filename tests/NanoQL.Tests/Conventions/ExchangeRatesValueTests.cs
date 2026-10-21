// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Currencies;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The exchange-rate registry is an immutable value (the S-rx plan's SRXR1): <see cref="ExchangeRates.Add"/> returns a
/// new registry; two registries are equal, with equal hashes, when they hold the same pairs, each with equal entries in
/// the same order; and <see cref="ExchangeRates.Clear"/> returns <see cref="ExchangeRates.Default"/>. The equality of
/// <see cref="ExchangeRate"/>, which nanoql adds, compares source, target, rate, type and chain.
/// </summary>
public class ExchangeRatesValueTests
{
    private static readonly Currency Eur = new EURCurrency();
    private static readonly Currency Usd = new USDCurrency();
    private static readonly Currency Gbp = new GBPCurrency();

    [Fact]
    public void AddReturnsACopy()
    {
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.October, 2026) };
        ExchangeRates before = ExchangeRates.Default;
        ExchangeRates after = before.Add(new ExchangeRate(Eur, Usd, 1.10));

        Assert.NotSame(before, after);
        Assert.Throws<NanoQLException>(() => before.Lookup(Eur, Usd, context));
        Assert.Equal(1.10, after.Lookup(Eur, Usd, context).Rate);

        // a later rate on the pair goes first, the earlier one kept behind it
        ExchangeRates later = after.Add(new ExchangeRate(Usd, Eur, 1.0 / 1.20), new Date(1, Month.October, 2026));
        Assert.Equal(1.0 / 1.20, later.Lookup(Eur, Usd, context, type: ExchangeRateType.Direct).Rate);
        Assert.Equal(1.10, later.Lookup(Eur, Usd, context, new Date(30, Month.September, 2026)).Rate);
        Assert.Equal(1.10, after.Lookup(Eur, Usd, context).Rate);
    }

    [Fact]
    public void EqualityAndHashAreByContent()
    {
        var start = new Date(1, Month.January, 2026);
        ExchangeRates a = ExchangeRates.Default.Add(new ExchangeRate(Eur, Usd, 1.10), start).Add(new ExchangeRate(Eur, Gbp, 0.80));
        ExchangeRates b = ExchangeRates.Default.Add(new ExchangeRate(new EURCurrency(), new USDCurrency(), 1.10), start)
            .Add(new ExchangeRate(new EURCurrency(), new GBPCurrency(), 0.80));
        Assert.True(a.Equals(b));
        Assert.True(a.Equals((object)b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());

        // the pairs' order of addition doesn't count; an entry's order on one pair does, and so do its dates
        ExchangeRates swapped = ExchangeRates.Default.Add(new ExchangeRate(Eur, Gbp, 0.80)).Add(new ExchangeRate(Eur, Usd, 1.10), start);
        Assert.Equal(a, swapped);
        ExchangeRates twice = ExchangeRates.Default.Add(new ExchangeRate(Eur, Usd, 1.10)).Add(new ExchangeRate(Eur, Usd, 1.20));
        ExchangeRates twiceReversed = ExchangeRates.Default.Add(new ExchangeRate(Eur, Usd, 1.20)).Add(new ExchangeRate(Eur, Usd, 1.10));
        Assert.NotEqual(twice, twiceReversed);
        Assert.NotEqual(a, ExchangeRates.Default.Add(new ExchangeRate(Eur, Usd, 1.10)).Add(new ExchangeRate(Eur, Gbp, 0.80)));
        Assert.NotEqual(a, ExchangeRates.Default.Add(new ExchangeRate(Eur, Usd, 1.10), start));
        Assert.False(a.Equals(null));

        // the rates' own equality: source, target, rate, type and chain
        var r = new ExchangeRate(Eur, Usd, 1.10);
        Assert.Equal(r, new ExchangeRate(new EURCurrency(), new USDCurrency(), 1.10));
        Assert.NotEqual(r, new ExchangeRate(Usd, Eur, 1.10));
        Assert.NotEqual(r, new ExchangeRate(Eur, Usd, 1.11));
        ExchangeRate chain = ExchangeRate.Chain(r, new ExchangeRate(Eur, Gbp, 0.80));
        Assert.Equal(chain, ExchangeRate.Chain(new ExchangeRate(Eur, Usd, 1.10), new ExchangeRate(Eur, Gbp, 0.80)));
        Assert.Equal(chain.GetHashCode(), ExchangeRate.Chain(new ExchangeRate(Eur, Usd, 1.10), new ExchangeRate(Eur, Gbp, 0.80)).GetHashCode());
        Assert.NotEqual(chain, new ExchangeRate(chain.Source, chain.Target, chain.Rate));
        Assert.NotEqual(chain, ExchangeRate.Chain(new ExchangeRate(Eur, Usd, 1.10), new ExchangeRate(Gbp, Eur, 1.0 / 0.80)));
    }

    [Fact]
    public void ClearReturnsTheKnownRates()
    {
        ExchangeRates added = ExchangeRates.Default.Add(new ExchangeRate(Eur, Usd, 1.10));
        Assert.Same(ExchangeRates.Default, added.Clear());
        Assert.NotEqual(ExchangeRates.Default, added);

        // QuantLib's known rates: the euro's legacy currencies, from 1999 (GRD from 2001)
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.October, 2026) };
        Assert.Equal(1936.27, ExchangeRates.Default.Lookup(Eur, new ITLCurrency(), context, type: ExchangeRateType.Direct).Rate);
        Assert.Equal(1000.0, ExchangeRates.Default.Lookup(new PEICurrency(), new PEHCurrency(), context).Rate);
    }
}
