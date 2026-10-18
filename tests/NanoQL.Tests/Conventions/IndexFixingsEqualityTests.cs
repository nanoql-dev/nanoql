// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="IndexFixings"/>' value equality, which QuantLib's <c>IndexManager</c> doesn't have: two stores are equal
/// when they hold the same names, by QuantLib's ASCII case-insensitive comparison, each with an equal history, whatever
/// order the fixings were added in and whichever spelling was kept; the hash follows.
/// </summary>
public class IndexFixingsEqualityTests
{
    private static readonly Date D1 = new(10, Month.January, 2024);
    private static readonly Date D2 = new(11, Month.January, 2024);

    [Fact]
    public void StoresCompareByNameAndHistory()
    {
        IborIndex lower = Index("Euribor"), upper = Index("EURIBOR"), other = Index("Other");

        IndexFixings a = IndexFixings.Empty.AddFixing(lower, D1, 0.01).AddFixing(lower, D2, 0.02).AddFixing(other, D1, 0.5);
        IndexFixings b = IndexFixings.Empty.AddFixing(other, D1, 0.5).AddFixings(upper, [D2, D1], [0.02, 0.01]);

        Assert.True(a.Equals(b));
        Assert.True(a.Equals((object)b));
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
        Assert.Equal(["Euribor6M Actual/360", "Other6M Actual/360"], a.Histories());
        Assert.Equal(["EURIBOR6M Actual/360", "Other6M Actual/360"], b.Histories());

        Assert.False(a.Equals(a.AddFixing(lower, D1, 0.011, forceOverwrite: true)));
        Assert.False(a.Equals(a.AddFixing(lower, new Date(12, Month.January, 2024), 0.02)));
        Assert.False(a.Equals(a.ClearFixings(other)));
        Assert.False(a.Equals(null));
        Assert.True(IndexFixings.Empty.Equals(IndexFixings.Empty.AddFixing(lower, D1, 0.01).ClearFixings(upper)));
    }

    [Fact]
    public void AnEmptyHistoryCounts()
    {
        IndexFixings empty = IndexFixings.Empty.AddFixings(Index("Euribor"), [], []);

        Assert.Equal(["Euribor6M Actual/360"], empty.Histories());
        Assert.False(empty.Equals(IndexFixings.Empty));
        Assert.True(empty.ClearFixings(Index("euribor")).Equals(IndexFixings.Empty));
    }

    private static IborIndex Index(string family) => new(
        family, new Period(6, TimeUnit.Months), 2, null, new TARGET(), BusinessDayConvention.ModifiedFollowing, true,
        new Actual360());
}
