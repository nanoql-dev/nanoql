// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// <see cref="ZeroCouponInflationSwapHelper"/>'s dates and refusals (<c>inflationhelpers.cpp:50-128</c>). A flat
/// observation needs its fixing period's start alone, so the earliest, latest and pillar dates are that start; an
/// interpolated observation whose lag less the index's period is shorter than the index's availability, or a custom
/// pillar outside its fixing dates, is an <see cref="ArgumentException"/> naming the argument, with QuantLib's text.
/// </summary>
public class ZeroCouponInflationSwapHelperTests
{
    private static readonly Date Today = new(15, Month.June, 2024);

    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };

    private static readonly Date Maturity = new(17, Month.June, 2029);

    [Fact]
    public void AFlatHelpersDatesAreItsFixingPeriodsStart()
    {
        ZeroCouponInflationSwapHelper helper = Helper(new Period(3, TimeUnit.Months), CPIInterpolationType.Flat);

        var start = new Date(1, Month.March, 2029);
        Assert.Equal(start, helper.EarliestDate);
        Assert.Equal(start, helper.LatestDate);
        Assert.Equal(start, helper.PillarDate);
    }

    [Fact]
    public void ALagTheIndexCantObserveIsRefused()
    {
        ArgumentException e = Assert.Throws<ArgumentException>(
            () => Helper(new Period(1, TimeUnit.Months), CPIInterpolationType.Linear));

        Assert.Equal("swapObsLag", e.ParamName);
        Assert.StartsWith(
            "inconsistency between swap observation lag 1M, index period 1M and index availability 1M: need "
            + "(obsLag-index period) >= availLag",
            e.Message,
            StringComparison.Ordinal);
    }

    [Fact]
    public void ACustomPillarOutsideTheFixingDatesIsRefused()
    {
        ArgumentException e = Assert.Throws<ArgumentException>(
            () => Helper(new Period(3, TimeUnit.Months), CPIInterpolationType.Linear, PillarChoice.CustomDate, new Date(1, Month.January, 2029)));

        Assert.Equal("customPillarDate", e.ParamName);
        Assert.StartsWith(
            "pillar date (January 1st, 2029) must be later than or equal to the instrument's earliest date (March 1st, 2029)",
            e.Message,
            StringComparison.Ordinal);
    }

    private static ZeroCouponInflationSwapHelper Helper(
        Period lag,
        CPIInterpolationType interpolation,
        PillarChoice pillar = PillarChoice.LastRelevantDate,
        Date? customPillar = null) =>
        new(
            0.03,
            lag,
            Maturity,
            new UnitedKingdom(),
            BusinessDayConvention.ModifiedFollowing,
            new Actual365Fixed(),
            new UKRPI(),
            interpolation,
            Context,
            pillar,
            customPillar);
}
