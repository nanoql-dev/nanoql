// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// QuantLib's <c>IndexManager</c> is a mutable singleton; nanoql's <see cref="IndexFixings"/> is an immutable value in
/// the evaluation context. Three behaviours differ.
/// </summary>
public class IndexFixingsTests
{
    private static readonly IborIndex Euribor = new(
        "Euribor", new Period(6, TimeUnit.Months), 2, null, new TARGET(), BusinessDayConvention.ModifiedFollowing, true,
        new Actual360());

    private static readonly Date Thursday = new(11, Month.January, 2024);
    private static readonly Date Friday = new(12, Month.January, 2024);
    private static readonly Date Saturday = new(13, Month.January, 2024);

    /// <summary>
    /// A refused batch changes nothing. QuantLib's <c>addFixings</c> stores each valid fixing as it goes, and its read
    /// of the current value (<c>h[*dBegin]</c>) inserts a null entry at the invalid date, before the checks after the
    /// loop throw (<c>indexmanager.hpp:76-111</c>), so the store keeps the valid fixings and a null entry.
    /// </summary>
    [Fact]
    public void ARefusedBatchChangesNothing()
    {
        IndexFixings store = IndexFixings.Empty.AddFixing(Euribor, Thursday, 0.01);

        Assert.Throws<ArgumentException>(() => store.AddFixings(Euribor, [Friday, Saturday], [0.02, 0.03]));
        Assert.Throws<ArgumentException>(() => store.AddFixings(Euribor, [Friday, Thursday], [0.02, 0.05]));

        var context = new EvaluationContext { EvaluationDate = Friday, Fixings = store };
        Assert.Equal([Thursday], Euribor.TimeSeries(context).Dates());
        Assert.False(Euribor.HasHistoricalFixing(Friday, context));
        Assert.Equal(store, IndexFixings.Empty.AddFixing(Euribor, Thursday, 0.01));

        // With forceOverwrite the duplicate is replaced, and the batch is kept whole.
        IndexFixings forced = store.AddFixings(Euribor, [Friday, Thursday], [0.02, 0.05], forceOverwrite: true);
        Assert.Equal([0.05, 0.02], Euribor.TimeSeries(context with { Fixings = forced }).Values());
    }

    /// <summary>
    /// Reading never writes. QuantLib's <c>hasHistoricalFixing</c> reads its history through the non-const
    /// <c>operator[]</c>, which inserts a null entry at a missing date (<c>indexmanager.cpp:75</c>,
    /// <c>timeseries.hpp:102-105</c>), and <c>getHistory</c>, behind <c>pastFixing</c> and <c>timeSeries()</c>, inserts
    /// an empty history for a missing name (<c>indexmanager.cpp:29</c>, <c>index.hpp:73</c>), through a <c>mutable</c>
    /// map (<c>indexmanager.hpp:60</c>).
    /// </summary>
    [Fact]
    public void ReadingNeverWrites()
    {
        IndexFixings store = IndexFixings.Empty.AddFixing(Euribor, Thursday, 0.01);
        var context = new EvaluationContext { EvaluationDate = Friday, Fixings = store };
        var other = new IborIndex(
            "Other", new Period(3, TimeUnit.Months), 2, null, new TARGET(), BusinessDayConvention.ModifiedFollowing,
            true, new Actual360());

        Assert.False(Euribor.HasHistoricalFixing(Friday, context));
        Assert.Null(Euribor.PastFixing(Friday, context));
        Assert.False(other.HasHistoricalFixing(Friday, context));
        Assert.Null(other.PastFixing(Friday, context));
        Assert.True(other.TimeSeries(context).Empty);

        Assert.Equal([Euribor.Name], store.Histories());
        Assert.Equal([Thursday], Euribor.TimeSeries(context).Dates());
        Assert.Equal(IndexFixings.Empty.AddFixing(Euribor, Thursday, 0.01), store);
    }

    /// <summary>
    /// A fixing equal to QuantLib's null sentinel, <c>Null&lt;Real&gt;()</c> (3.4028234663852886e38, the largest
    /// <c>float</c>), is a value. QuantLib can't tell it from a missing fixing: an add treats it as missing, so a later
    /// add of another value is accepted (<c>indexmanager.hpp:84-85</c>), and <c>hasHistoricalFixing</c> and
    /// <c>fixing</c> read it as missing (<c>indexmanager.cpp:75</c>, <c>interestrateindex.cpp:80-81</c>). nanoql's
    /// missing fixing is <see langword="null"/>, so the sentinel is stored like any other number, as an
    /// <c>int.MaxValue</c> stays a value where QuantLib's <c>Null&lt;Natural&gt;()</c> means "not set".
    /// </summary>
    [Fact]
    public void QuantLibsNullSentinelIsAValue()
    {
        const double NullReal = 3.4028234663852886e38;
        IndexFixings store = IndexFixings.Empty.AddFixing(Euribor, Thursday, NullReal);
        var context = new EvaluationContext { EvaluationDate = Friday, Fixings = store };

        Assert.True(Euribor.HasHistoricalFixing(Thursday, context));
        Assert.Equal(NullReal, Euribor.PastFixing(Thursday, context));
        Assert.Equal(NullReal, Euribor.Fixing(Thursday, context));
        Assert.Throws<ArgumentException>(() => store.AddFixing(Euribor, Thursday, 0.02));
    }

    /// <summary>
    /// Fewer values than dates are refused before anything is stored. QuantLib's loop reads one value per date through
    /// the value iterator (<c>indexmanager.hpp:88</c>), past the end of a shorter range, which is undefined. Surplus
    /// values are ignored, as QuantLib ignores them.
    /// </summary>
    [Fact]
    public void FewerValuesThanDatesAreRefused()
    {
        ArgumentException e = Assert.Throws<ArgumentException>(
            () => IndexFixings.Empty.AddFixings(Euribor, [Thursday, Friday], [0.01]));
        Assert.Equal("fixings", e.ParamName);

        IndexFixings store = IndexFixings.Empty.AddFixings(Euribor, [Thursday], [0.01, 0.02]);
        var context = new EvaluationContext { EvaluationDate = Friday, Fixings = store };
        Assert.Equal([0.01], Euribor.TimeSeries(context).Values());
    }
}
