// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Period equality never throws: QuantLib's <c>==</c> is <c>!(p1 &lt; p2 || p2 &lt; p1)</c> and throws where the order
/// can't be decided (1M against 30D); nanoql's returns <see langword="false"/> there. Ordering still throws, as
/// QuantLib's does.
/// </summary>
public class PeriodEqualityTests
{
    [Fact]
    public void UndecidablePairsCompareUnequalWithoutThrowing()
    {
        var oneMonth = new Period(1, TimeUnit.Months);
        var thirtyDays = new Period(30, TimeUnit.Days);

        Assert.False(oneMonth == thirtyDays);
        Assert.True(oneMonth != thirtyDays);
        Assert.False(oneMonth.Equals(thirtyDays));
        Assert.False(new Period(1, TimeUnit.Years).Equals((object)new Period(365, TimeUnit.Days)));
    }

    [Fact]
    public void OrderingStillThrowsOnUndecidablePairs()
    {
        var oneMonth = new Period(1, TimeUnit.Months);
        var thirtyDays = new Period(30, TimeUnit.Days);

        Assert.Throws<NanoQLException>(() => oneMonth < thirtyDays);
        Assert.Throws<NanoQLException>(() => oneMonth > thirtyDays);
        Assert.Throws<NanoQLException>(() => oneMonth.CompareTo(thirtyDays));
    }

    [Fact]
    public void CompareToFollowsQuantLibsOrderingWhereItIsDecidable()
    {
        Assert.Equal(-1, new Period(1, TimeUnit.Months).CompareTo(new Period(1, TimeUnit.Years)));
        Assert.Equal(1, new Period(2, TimeUnit.Weeks).CompareTo(new Period(13, TimeUnit.Days)));
        Assert.Equal(0, new Period(1, TimeUnit.Years).CompareTo(new Period(12, TimeUnit.Months)));
        Assert.Equal(0, new Period(0, TimeUnit.Days).CompareTo(new Period(0, TimeUnit.Years)));
        Assert.Equal(-1, new Period(-1, TimeUnit.Days).CompareTo(new Period(1, TimeUnit.Months)));

        // The non-generic form: a null sorts first, and another type is refused.
        Assert.Equal(1, new Period(1, TimeUnit.Days).CompareTo((object?)null));
        Assert.Equal(0, new Period(7, TimeUnit.Days).CompareTo((object)new Period(1, TimeUnit.Weeks)));
        Assert.Throws<ArgumentException>(() => new Period(1, TimeUnit.Days).CompareTo((object)1));
    }

    [Fact]
    public void EveryZeroLengthPeriodIsEqualWithOneHash()
    {
        Period[] zeros = [.. Enum.GetValues<TimeUnit>().Select(u => new Period(0, u)), default];

        foreach (Period a in zeros)
        {
            foreach (Period b in zeros)
            {
                Assert.True(a == b, $"{a.Length} {a.Units} and {b.Length} {b.Units}");
                Assert.Equal(a.GetHashCode(), b.GetHashCode());
            }
        }

        Assert.False(new Period(0, TimeUnit.Days) == new Period(1, TimeUnit.Days));
    }

    [Fact]
    public void ExactlyComparablePeriodsCompareNormalisedWithEqualHashes()
    {
        (Period A, Period B)[] equal =
        [
            (new Period(1, TimeUnit.Years), new Period(12, TimeUnit.Months)),
            (new Period(2, TimeUnit.Weeks), new Period(14, TimeUnit.Days)),
            (new Period(-3, TimeUnit.Years), new Period(-36, TimeUnit.Months)),
        ];
        foreach ((Period a, Period b) in equal)
        {
            Assert.True(a == b, $"{a} and {b}");
            Assert.True(a.Equals((object)b));
            Assert.Equal(a.GetHashCode(), b.GetHashCode());
        }

        Assert.False(new Period(1, TimeUnit.Years) == new Period(13, TimeUnit.Months));
        Assert.False(new Period(1, TimeUnit.Weeks) == new Period(8, TimeUnit.Days));
    }

    [Fact]
    public void PeriodsHashConsistentlyInHashSets()
    {
        var set = new HashSet<Period> { new(1, TimeUnit.Years), new(6, TimeUnit.Months) };

        Assert.Contains(new Period(12, TimeUnit.Months), set);
        Assert.DoesNotContain(new Period(180, TimeUnit.Days), set);
    }
}
