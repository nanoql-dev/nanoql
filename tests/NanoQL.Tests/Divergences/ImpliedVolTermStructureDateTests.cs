// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The implied Black-volatility structure on a day counter whose year fractions don't add, and before its original's
/// reference date. QuantLib reads its original between the reference date's time on the original's clock and that time
/// plus the query's (<c>impliedvoltermstructure.hpp:101-109</c>): on Thirty360, from 15 October 2026 at 20% and implied
/// at 31 January 2027, its variance at 28 February 2027 is 0.0031111111111111114, 3.70% above the original's forward
/// variance by date, 0.0030000000000000009, and at 30 June 2027 0.67% above it; implied at 30 November 2026, its
/// variance at 31 March 2027 is 0.83% below it (QuantLib 1.44). nanoql reads a date by date where the sum misses the
/// date's own time on the original's clock, and keeps QuantLib's bits elsewhere, as on every date of Actual/365
/// (Fixed). Implied at 15 July 2026, before the original's date, QuantLib's variance at 15 January 2027 is
/// 0.02016438356164384, the original's variance read three months before its own date added; nanoql's accrues none
/// before it, the original's 0.01008219178082192, and at 15 September 2026 none at all, where QuantLib refuses the
/// negative time; on Thirty360 from 31 August 2026, a date the sum misses is read by date from the original's own date.
/// </summary>
public class ImpliedVolTermStructureDateTests
{
    private static readonly Date Origin = new(15, Month.October, 2026);

    [Theory]
    [InlineData(Thirty360Convention.USA)]
    [InlineData(Thirty360Convention.BondBasis)]
    public void OnThirty360ADateReadsTheOriginalsVarianceByDate(Thirty360Convention convention)
    {
        var dc = new Thirty360(convention);
        var original = new BlackConstantVol(Origin, null, 0.2, dc);

        var implied = new ImpliedVolTermStructure(original, new Date(31, Month.January, 2027));
        Date february = new(28, Month.February, 2027), june = new(30, Month.June, 2027), december = new(31, Month.December, 2027);
        Assert.Equal(0.0030000000000000009, implied.BlackVariance(february, 100.0));
        Assert.Equal(0.016555555555555559, implied.BlackVariance(june, 100.0));
        Assert.Equal(original.BlackForwardVariance(implied.ReferenceDate, june, 100.0), implied.BlackVariance(june, 100.0));
        double t = implied.TimeFromReference(february);
        Assert.Equal(Math.Sqrt(0.0030000000000000009 / t), implied.BlackVol(february, 100.0));
        Assert.Equal(0.016555555555555559 - 0.0030000000000000009, implied.BlackForwardVariance(february, june, 100.0));

        // where the sum is the date's time, QuantLib's bits: QuantLib's 0.036666666666666667 and its 20%
        Assert.Equal(0.036666666666666667, implied.BlackVariance(december, 100.0));
        Assert.Equal(0.20000000000000001, implied.BlackVol(december, 100.0));

        // a time keeps QuantLib's sum: 28 February's time gives QuantLib's 0.0031111111111111114
        Assert.Equal(0.0031111111111111114, implied.BlackVariance(t, 100.0));

        var fromNovember = new ImpliedVolTermStructure(original, new Date(30, Month.November, 2026));
        Assert.Equal(0.01344444444444445, fromNovember.BlackVariance(new Date(31, Month.March, 2027), 100.0));
        Assert.Equal(0.0097777777777777811, fromNovember.BlackVariance(february, 100.0));
    }

    [Fact]
    public void OnAnAdditiveDayCounterEveryDateKeepsQuantLibsBits()
    {
        var original = new BlackConstantVol(Origin, null, 0.2, new Actual365Fixed());
        var implied = new ImpliedVolTermStructure(original, new Date(31, Month.January, 2027));
        Assert.Equal(0.0030684931506849318, implied.BlackVariance(new Date(28, Month.February, 2027), 100.0));
        Assert.Equal(0.016438356164383564, implied.BlackVariance(new Date(30, Month.June, 2027), 100.0));
        Assert.Equal(0.036602739726027414, implied.BlackVariance(new Date(31, Month.December, 2027), 100.0));
        Assert.Equal(0.20000000000000004, implied.BlackVol(new Date(31, Month.December, 2027), 100.0));
    }

    [Fact]
    public void AReferenceDateBeforeTheOriginalsAccruesNothingBeforeIt()
    {
        var original = new BlackConstantVol(Origin, null, 0.2, new Actual365Fixed());
        var implied = new ImpliedVolTermStructure(original, new Date(15, Month.July, 2026));

        Date january = new(15, Month.January, 2027), october = new(15, Month.October, 2027);
        Assert.Equal(0.01008219178082192, implied.BlackVariance(january, 100.0), 1e-17);
        Assert.Equal(0.040000000000000008, implied.BlackVariance(october, 100.0), 1e-17);
        Assert.True(Math.Abs(0.02016438356164384 - implied.BlackVariance(january, 100.0)) > 1e-3);
        Assert.Equal(0.0, implied.BlackVariance(new Date(15, Month.September, 2026), 100.0));

        // on Thirty360 from 31 August 2026, where the sum misses the date (134/360 against 28 February's 133/360 on the
        // original's clock), the date read by date, none accruing before the original's date
        var thirty = new BlackConstantVol(Origin, null, 0.2, new Thirty360(Thirty360Convention.USA));
        var fromAugust = new ImpliedVolTermStructure(thirty, new Date(31, Month.August, 2026));
        Date february = new(28, Month.February, 2027);
        Assert.Equal(thirty.BlackVariance(february, 100.0), fromAugust.BlackVariance(february, 100.0));
        Assert.Equal(0.04 * 133.0 / 360.0, fromAugust.BlackVariance(february, 100.0), 1e-17);
        Assert.Equal(0.0, fromAugust.BlackVariance(new Date(30, Month.September, 2026), 100.0));
    }
}
