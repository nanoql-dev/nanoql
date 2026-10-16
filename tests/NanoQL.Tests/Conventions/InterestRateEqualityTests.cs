// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="InterestRate"/> is a value: two rates are equal when their rate, their day counter (by name), their
/// compounding and the frequency the compounding uses are equal. QuantLib's <c>InterestRate</c> has no
/// <c>operator==</c>.
/// </summary>
public class InterestRateEqualityTests
{
    [Fact]
    public void EqualRatesCompareEqualWithEqualHashes()
    {
        var a = new InterestRate(0.03, new Actual360(), Compounding.Compounded, Frequency.Semiannual);
        var b = new InterestRate(0.03, new Actual360(), Compounding.Compounded, Frequency.Semiannual);

        Assert.NotSame(a.DayCounter, b.DayCounter);
        Assert.Equal(a, b);
        Assert.True(a == b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());

        // Day counters compare by name: Thirty360's ISMA and BondBasis conventions share one.
        Assert.Equal(
            new InterestRate(0.03, new Thirty360(Thirty360Convention.ISMA), Compounding.Simple, Frequency.Annual),
            new InterestRate(0.03, new Thirty360(Thirty360Convention.BondBasis), Compounding.Simple, Frequency.Annual));
    }

    [Fact]
    public void ADifferentRateDayCounterCompoundingOrFrequencyComparesUnequal()
    {
        var a = new InterestRate(0.03, new Actual360(), Compounding.Compounded, Frequency.Semiannual);

        Assert.NotEqual(a, new InterestRate(0.031, new Actual360(), Compounding.Compounded, Frequency.Semiannual));
        Assert.NotEqual(a, new InterestRate(0.03, new Actual365Fixed(), Compounding.Compounded, Frequency.Semiannual));
        Assert.NotEqual(a, new InterestRate(0.03, new Actual360(), Compounding.Continuous, Frequency.Semiannual));
        Assert.NotEqual(a, new InterestRate(0.03, new Actual360(), Compounding.Compounded, Frequency.Quarterly));
        Assert.False(a.Equals(null));
    }

    [Fact]
    public void TheFrequencyGivenToASimpleOrContinuousRateDoesNotMatter()
    {
        // Only the compounded rules use the frequency; for the others QuantLib keeps none.
        foreach (Compounding comp in new[] { Compounding.Simple, Compounding.Continuous })
        {
            var annual = new InterestRate(0.03, new Actual360(), comp, Frequency.Annual);
            var monthly = new InterestRate(0.03, new Actual360(), comp, Frequency.Monthly);

            Assert.Equal(Frequency.NoFrequency, annual.Frequency);
            Assert.Equal(annual, monthly);
            Assert.Equal(annual.GetHashCode(), monthly.GetHashCode());
        }
    }
}
