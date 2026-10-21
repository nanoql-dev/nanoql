// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The results records compare by value: synthesized record equality would compare their arrays and dictionaries by
/// reference, so <see cref="InstrumentResults"/> and <see cref="SwapResults"/> write it out, element by element, and a
/// collection among the additional results compares element by element too.
/// </summary>
public class ResultsEqualityTests
{
    private static SwapResults Sample() => new()
    {
        NPV = 12.5,
        ErrorEstimate = null,
        ValuationDate = new Date(15, Month.October, 2026),
        AdditionalResults = ImmutableSortedDictionary<string, object>.Empty
            .Add("spread", 0.001)
            .Add("legs", new[] { 1.0, 2.0 }),
        LegNPV = [-100.0, 112.5],
        LegBPS = [-0.4, 0.45],
        StartDiscounts = [null, null],
        EndDiscounts = [0.9, 0.9],
        NpvDateDiscount = 1.0,
    };

    [Fact]
    public void EqualResultsCompareEqualWithEqualHashes()
    {
        SwapResults a = Sample(), b = Sample();

        Assert.NotSame(a, b);
        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.False(a != b);
        Assert.True(a.Equals((object)b));
        Assert.True(((InstrumentResults)a).Equals(b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());

        var x = new InstrumentResults { NPV = 1.0, ValuationDate = new Date(1, Month.January, 2026) };
        var y = new InstrumentResults { NPV = 1.0, ValuationDate = new Date(1, Month.January, 2026) };
        Assert.Equal(x, y);
        Assert.Equal(x.GetHashCode(), y.GetHashCode());
        Assert.Equal(new InstrumentResults(), new InstrumentResults());
        Assert.Equal(new SwapResults(), new SwapResults());
    }

    [Fact]
    public void ADifferentLegValueOrAdditionalResultComparesUnequal()
    {
        SwapResults a = Sample();

        Assert.NotEqual(a, a with { LegNPV = [-100.0, 112.6] });
        Assert.NotEqual(a, a with { LegBPS = [-0.4] });
        Assert.NotEqual(a, a with { StartDiscounts = [null, 1.0] });
        Assert.NotEqual(a, a with { EndDiscounts = [0.9, 0.8] });
        Assert.NotEqual(a, a with { NpvDateDiscount = null });
        Assert.NotEqual(a, a with { NPV = 12.6 });
        Assert.NotEqual(a, a with { ErrorEstimate = 0.0 });
        Assert.NotEqual(a, a with { ValuationDate = null });
        Assert.NotEqual(a, a with { AdditionalResults = a.AdditionalResults.SetItem("spread", 0.002) });
        Assert.NotEqual(a, a with { AdditionalResults = a.AdditionalResults.Remove("spread") });
        Assert.NotEqual(a, a with { AdditionalResults = a.AdditionalResults.Remove("spread").Add("other", 0.001) });
        Assert.False(a.Equals(null));
        Assert.False(a == null);
    }

    [Fact]
    public void FloatFloatSwapResultsCompareTheSwapsResultsAndTheFairSpreads()
    {
        static FloatFloatSwapResults Results() => new()
        {
            NPV = 12.5,
            LegNPV = [-100.0, 112.5],
            LegBPS = [-0.4, 0.45],
            FairSpread1 = 0.002,
            FairSpread2 = null,
        };

        FloatFloatSwapResults a = Results(), b = Results();
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, a with { LegNPV = [-100.0, 112.6] });
        Assert.NotEqual(a, a with { FairSpread1 = 0.0021 });
        Assert.NotEqual(a, a with { FairSpread2 = 0.0 });
        Assert.False(a.Equals(null));
    }

    [Fact]
    public void CrossCurrencySwapResultsCompareTheSwapsResultsAndTheInCurrencyArrays()
    {
        static CrossCurrencySwapResults Results() => new()
        {
            NPV = 12.5,
            LegNPV = [-100.0, 112.5],
            LegBPS = [-0.4, 0.45],
            InCcyLegNPV = [-90.0, 112.5],
            InCcyLegBPS = [-0.36, 0.45],
            NpvDateDiscounts = [0.99, 0.98],
        };

        CrossCurrencySwapResults a = Results(), b = Results();
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, a with { LegNPV = [-100.0, 112.6] });
        Assert.NotEqual(a, a with { InCcyLegNPV = [-90.0, 112.6] });
        Assert.NotEqual(a, a with { InCcyLegBPS = [-0.36, 0.46] });
        Assert.NotEqual(a, a with { NpvDateDiscounts = [0.99, 0.97] });
        Assert.NotEqual(a, a with { NpvDateDiscounts = [] });
        Assert.False(a.Equals(null));
    }

    [Fact]
    public void ConstNotionalCrossCurrencySwapResultsCompareTheirFairSpreadsAndRates()
    {
        static ConstNotionalCrossCurrencyBasisSwapResults Basis() => new()
        {
            NPV = 12.5,
            InCcyLegNPV = [-90.0, 112.5],
            FairPaySpread = 0.002,
            FairRecSpread = null,
        };
        static ConstNotionalCrossCurrencyFixedVsFloatingSwapResults FixedVsFloating() => new()
        {
            NPV = 12.5,
            InCcyLegNPV = [-90.0, 112.5],
            FairFixedRate = 0.03,
            FairSpread = null,
        };

        ConstNotionalCrossCurrencyBasisSwapResults a = Basis(), b = Basis();
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, a with { InCcyLegNPV = [-90.0, 112.6] });
        Assert.NotEqual(a, a with { FairPaySpread = 0.0021 });
        Assert.NotEqual(a, a with { FairRecSpread = 0.0 });
        Assert.False(a.Equals(null));

        ConstNotionalCrossCurrencyFixedVsFloatingSwapResults c = FixedVsFloating(), d = FixedVsFloating();
        Assert.Equal(c, d);
        Assert.Equal(c.GetHashCode(), d.GetHashCode());
        Assert.NotEqual(c, c with { InCcyLegNPV = [-90.0, 112.6] });
        Assert.NotEqual(c, c with { FairFixedRate = 0.031 });
        Assert.NotEqual(c, c with { FairSpread = 0.0 });
        Assert.False(c.Equals(null));
    }

    [Fact]
    public void MtMCrossCurrencyBasisSwapResultsCompareTheirFairSpreadsAndResets()
    {
        static NanoQL.Experimental.MtMCrossCurrencyBasisSwapResults Results() => new()
        {
            NPV = 12.5,
            InCcyLegNPV = [-90.0, 112.5],
            FairFxBaseSpread = 0.001,
            FairFxQuoteSpread = null,
            FxResetRates = [1.1, 1.104],
            FxResetNotionals = [1.1e6, 1.104e6],
        };

        NanoQL.Experimental.MtMCrossCurrencyBasisSwapResults a = Results(), b = Results();
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.NotEqual(a, a with { InCcyLegNPV = [-90.0, 112.6] });
        Assert.NotEqual(a, a with { FairFxBaseSpread = 0.0011 });
        Assert.NotEqual(a, a with { FairFxQuoteSpread = 0.0 });
        Assert.NotEqual(a, a with { FxResetRates = [1.1, 1.105] });
        Assert.NotEqual(a, a with { FxResetNotionals = [1.1e6] });
        Assert.Equal(a with { FxResetRates = [] }, a with { FxResetRates = default });
        Assert.False(a.Equals(null));
    }

    [Fact]
    public void ResultsOfDifferentTypesCompareUnequal()
    {
        var instrument = new InstrumentResults { NPV = 1.0 };
        var swap = new SwapResults { NPV = 1.0 };

        Assert.False(instrument.Equals(swap));
        Assert.False(swap.Equals(instrument));
        Assert.NotEqual<InstrumentResults>(instrument, swap);
    }

    [Fact]
    public void NaNComparesAsSynthesizedEqualityDoes()
    {
        // EqualityComparer<double>.Default, which synthesized record equality uses, treats NaN as equal to itself.
        Assert.True(EqualityComparer<double?>.Default.Equals(double.NaN, double.NaN));

        var a = new SwapResults { NPV = double.NaN, LegNPV = [double.NaN, 1.0] };
        var b = new SwapResults { NPV = double.NaN, LegNPV = [double.NaN, 1.0] };
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());

        var c = new InstrumentResults
        {
            AdditionalResults = ImmutableSortedDictionary<string, object>.Empty.Add("x", double.NaN),
        };
        var d = new InstrumentResults
        {
            AdditionalResults = ImmutableSortedDictionary<string, object>.Empty.Add("x", double.NaN),
        };
        Assert.Equal(c, d);
        Assert.Equal(c.GetHashCode(), d.GetHashCode());
    }

    [Fact]
    public void ACollectionAmongTheAdditionalResultsComparesElementByElement()
    {
        static InstrumentResults With(object value) => new()
        {
            AdditionalResults = ImmutableSortedDictionary<string, object>.Empty.Add("values", value),
        };

        // Two distinct arrays with equal elements: a boxed array's own Equals would compare references.
        Assert.False(new[] { 1.0, 2.0 }.Equals(new[] { 1.0, 2.0 }));
        Assert.Equal(With(new[] { 1.0, 2.0 }), With(new[] { 1.0, 2.0 }));
        Assert.Equal(With(new[] { 1.0, 2.0 }).GetHashCode(), With(new[] { 1.0, 2.0 }).GetHashCode());

        // Nested collections, and collections of other types, compare element by element too.
        Assert.Equal(
            With(new List<double[]> { new[] { 1.0 }, new[] { 2.0, 3.0 } }),
            With(new List<double[]> { new[] { 1.0 }, new[] { 2.0, 3.0 } }));
        Assert.Equal(With(ImmutableArray.Create(1, 2)), With(new[] { 1, 2 }));

        Assert.NotEqual(With(new[] { 1.0, 2.0 }), With(new[] { 1.0, 3.0 }));
        Assert.NotEqual(With(new[] { 1.0, 2.0 }), With(new[] { 1.0, 2.0, 3.0 }));
        Assert.NotEqual(With(new[] { 1.0, 2.0 }), With(new[] { 1.0 }));
        Assert.NotEqual(With(new[] { 1.0 }), With(1.0));

        // A string is a value, not a collection of characters.
        Assert.Equal(With("abc"), With(new string("abc")));
        Assert.NotEqual(With("abc"), With(new[] { 'a', 'b', 'c' }));
    }
}
