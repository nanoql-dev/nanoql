// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Tests.Conventions;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A CPI leg's base. QuantLib's <c>CPILeg</c> gives each coupon <c>laggedFixing(baseDate + lag, lag)</c> and the
/// notional flow <c>laggedFixing(baseDate, 0M)</c> (<c>cpicoupon.cpp:112-121, 168-172</c>), so under a linear
/// observation the notional flow misses the coupons' base unless the base date starts its month. With neither a base
/// CPI nor a base date it gives every flow the base date start - lag (<c>:286-288</c>), so from a month-end start the
/// coupons miss <c>laggedFixing(start, lag)</c> too, since (start - lag) + lag loses the start's day (QuantLib's
/// defect, R8). nanoql's leg gives every flow one base: with neither, <c>laggedFixing(start, lag, interpolation)</c>,
/// QuantLib's own answer for the same leg built with that value as its explicit base CPI, which the wheel gives; with a
/// base date, the coupons' <c>laggedFixing(baseDate + lag, lag, interpolation)</c>, the notional flow included, which
/// QuantLib's <c>CPICashFlow</c> given that base fixing pays. On <see cref="InflationFixture"/>'s market, for a
/// two-year annual leg of 1,000,000 at 2%, linear, lagged three months: from 15 May 2024 with neither base, the base is
/// 310.30694493402456 and the notional flow pays 63,562.49459146323, where QuantLib's reads 310.34168149586054 and pays
/// 63,443.44998172313; from 31 May, the base is 310.882579387307 and the coupons pay 20,637.587250557182 and
/// 21,256.7148680739 and the notional flow 62,835.74340369502, where QuantLib's pay 20,642.36496136218,
/// 21,261.63591020305 and 62,844.226063026195 on the base date 29 February 2024; from 15 May with the base date
/// 15 March 2024, off the start less the lag, the base is 311.54246644409534 and the notional flow pays
/// 59,344.59661315317, where QuantLib's reads 311.5223403181773 and pays 59,413.036336353194.
/// </summary>
public class CpiLegBaseFixingTests
{
    [Fact]
    public void TheNotionalFlowSharesTheCouponsBase()
    {
        // QuantLib's: [20651.698924106084, 21271.249891829266, 63443.44998172313]
        AssertAmounts(
            new Date(15, Month.May, 2024),
            new Date(15, Month.May, 2026),
            310.30694493402456,
            [20651.698924106084, 21271.249891829266, 63562.49459146323]);
    }

    [Fact]
    public void AMonthEndStartKeepsItsDay()
    {
        // QuantLib's: [20642.36496136218, 21261.63591020305, 62844.226063026195]
        AssertAmounts(
            new Date(31, Month.May, 2024),
            new Date(31, Month.May, 2026),
            310.882579387307,
            [20637.587250557182, 21256.7148680739, 62835.74340369502]);
    }

    [Fact]
    public void ABaseDateGivesTheNotionalFlowTheCouponsBase()
    {
        // QuantLib's: [20569.797992488413, 21186.891932263065, 59413.036336353194], its notional flow on
        // laggedFixing(15 March 2024, 0M, Linear) = 311.5223403181773
        AssertAmounts(
            new Date(15, Month.May, 2024),
            new Date(15, Month.May, 2026),
            311.54246644409534,
            [20569.797992488413, 21186.891932263065, 59344.59661315317],
            new Date(15, Month.March, 2024));
    }

    private static void AssertAmounts(
        Date start, Date end, double expectedBase, double[] expected, Date? baseDate = null)
    {
        EvaluationContext context = InflationFixture.Context();
        var rpi = new UKRPI(InflationFixture.ZeroCurve());
        var lag = new Period(3, TimeUnit.Months);
        Calendar uk = new UnitedKingdom();
        var schedule = new Schedule(
            start,
            end,
            new Period(1, TimeUnit.Years),
            uk,
            BusinessDayConvention.Unadjusted,
            BusinessDayConvention.Unadjusted,
            DateGenerationRule.Forward,
            false,
            context);
        ImmutableArray<CashFlow> leg = new CPILeg(schedule, rpi, null, lag)
            .WithNotionals(1e6)
            .WithFixedRates(0.02)
            .WithPaymentDayCounter(new Actual365Fixed())
            .WithPaymentAdjustment(BusinessDayConvention.ModifiedFollowing)
            .WithPaymentCalendar(uk)
            .WithObservationInterpolation(CPIInterpolationType.Linear)
            .WithBaseDate(baseDate)
            .ToLeg();

        Date observed = baseDate is Date b ? b + lag : start;
        Assert.Equal(expectedBase, CPI.LaggedFixing(rpi, observed, lag, CPIInterpolationType.Linear, context));
        Assert.Equal(expected.Length, leg.Length);
        for (int i = 0; i < expected.Length; i++)
        {
            double amount = leg[i].Amount(context);
            Assert.True(Tolerance.Within(expected[i], amount), $"flow {i}: {amount} against {expected[i]}");
        }
    }
}
