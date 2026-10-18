// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A bootstrapped curve is always built afresh, under the context it is given. QuantLib's piecewise curve is a lazy
/// object that recalculates when a quote or the evaluation date changes, and its recalculation differs from a fresh
/// bootstrap in three ways: it starts from its previous data (<c>iterativebootstrap.hpp:211-218, 253-255</c>;
/// <c>globalbootstrap.hpp:340-347, 387-390, 409-411</c>), it
/// re-runs <c>initialize()</c>, which takes the pillar dates from the helpers, only on first use or for a moving curve
/// (<c>iterativebootstrap.hpp:224-231</c>), and a moving curve never re-runs <c>YieldTermStructure::update()</c>, which
/// re-sets its jumps (<c>piecewiseyieldcurve.hpp:220-231</c>, <c>yieldtermstructure.cpp:174-189</c>). nanoql's curve is
/// QuantLib's fresh curve in each case. The expected values are the wheel's.
/// </summary>
public class CurveRecalculationTests
{
    private static readonly DayCounter Act365 = new Actual365Fixed();

    /// <summary>
    /// After a quote change, QuantLib's curve re-bootstraps from its previous nodes, with the finite-difference
    /// Newton solver and brackets from the previous data; the result agrees with a fresh bootstrap within the
    /// bootstrap's accuracy, not to the last bit. nanoql's curve on the changed quote is the fresh curve: its last node
    /// differs from QuantLib's warm-started one by 7e-14.
    /// </summary>
    [Fact]
    public void ACurveOnAChangedQuoteIsBootstrappedAfresh()
    {
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.January, 2024) };

        var curve = new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(
            new Date(17, Month.January, 2024), Helpers(context, 0.037), Act365, context);

        // the wheel's fresh curve on the changed quote
        Assert.Equal([1.0, 0.9850599244789622, 0.940765015270587, 0.8329539084088842, 0.7245126933508962], curve.Data());

        // the wheel's curve, built on 3.2% and recalculated after the quote moved to 3.7%
        Assert.NotEqual(0.7245126933508267, curve.Data()[^1]);
    }

    /// <summary>
    /// After a quote change, QuantLib's globally bootstrapped curve starts its optimization from its previous nodes,
    /// the traits' guess from valid data; the result agrees with a fresh bootstrap within the optimizer's accuracy, not
    /// to the last bit. nanoql's curve on the changed quote is the fresh curve: its last node differs from QuantLib's
    /// warm-started one by 7e-16.
    /// </summary>
    [Fact]
    public void AGlobalCurveOnAChangedQuoteIsBootstrappedAfresh()
    {
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.January, 2024) };

        var curve = new PiecewiseYieldCurve<Discount, LogLinear, GlobalBootstrap>(
            new Date(17, Month.January, 2024), Helpers(context, 0.037), Act365, context);

        // the wheel's fresh curve on the changed quote
        Assert.Equal([1.0, 0.985059924478739, 0.9407650152705913, 0.8329539084088886, 0.72451269335083], curve.Data());

        // the wheel's curve, built on 3.2% and recalculated after the quote moved to 3.7%
        Assert.NotEqual(0.7245126933508307, curve.Data()[^1]);
    }

    /// <summary>
    /// A fixed-reference QuantLib curve on helpers whose dates follow the evaluation date keeps its first evaluation
    /// date's pillars after the date moves: the helpers re-date themselves, but the recalculation solves the nodes at
    /// the old pillars. nanoql's curve, built under the later context, re-dates its helpers and takes their pillars, as a
    /// fresh QuantLib curve does: the one-week deposit's pillar is 25 January, where the recalculated curve keeps 24
    /// January.
    /// </summary>
    [Fact]
    public void AFixedReferenceCurveTakesItsContextsPillars()
    {
        var today = new Date(15, Month.January, 2024);
        var first = new EvaluationContext { EvaluationDate = today };
        var later = new EvaluationContext { EvaluationDate = new Date(16, Month.January, 2024) };
        var dc = new Actual360();
        RateHelper[] helpers =
        [
            new DepositRateHelper(
                0.0382, new Period(1, TimeUnit.Weeks), 2, new TARGET(), BusinessDayConvention.Following, false, dc, first),
            new SwapRateHelper(
                0.0340,
                new Period(2, TimeUnit.Years),
                new TARGET(),
                Frequency.Annual,
                BusinessDayConvention.Unadjusted,
                new Thirty360(Thirty360Convention.BondBasis),
                new Euribor6M(),
                first),
        ];

        var curve = new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(today, helpers, dc, later);

        // the wheel's fresh curve at 16 January
        Assert.Equal([new Date(15, Month.January, 2024), new Date(25, Month.January, 2024), new Date(19, Month.January, 2026)], curve.Dates());
        Assert.Equal([1.0, 0.9989398451403876, 0.9350269466937013], curve.Data());

        // the wheel's curve built at 15 January and recalculated at 16 January
        Assert.NotEqual(new Date(24, Month.January, 2024), curve.Dates()[1]);
    }

    /// <summary>
    /// A moving QuantLib curve keeps its jumps' dates and times from its first reference date: built on 16 June 2025
    /// and moved to 15 January 2026, its 0.99 jump falls on the time the turn of 2025 had from June, early August 2026.
    /// nanoql's curve built under the later context dates its jumps from its own reference date, 31 December 2026 and
    /// 2027, as a fresh QuantLib curve does: its discount on 15 October 2026 is 0.98227…, where the moved curve's is
    /// 0.97245….
    /// </summary>
    [Fact]
    public void AMovingCurvesJumpsFollowItsReferenceDate()
    {
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.January, 2026) };

        var curve = PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>.Create(
            0, new TARGET(), Helpers(context, 0.032), Act365, context, jumps: [0.99, 0.98]);

        Assert.Equal([new Date(31, Month.December, 2026), new Date(31, Month.December, 2027)], curve.JumpDates());

        // the wheel's fresh curve at 15 January 2026, and its moved curve
        double discount = curve.Discount(new Date(15, Month.October, 2026));
        Assert.Equal(0.9822736891263767, discount);
        Assert.NotEqual(0.9724509522348563, discount);
    }

    // A six-month deposit and three swaps, the five-year swap quoted at the rate given.
    private static RateHelper[] Helpers(EvaluationContext context, double fiveYears) =>
    [
        new DepositRateHelper(
            0.030,
            new Period(6, TimeUnit.Months),
            2,
            new TARGET(),
            BusinessDayConvention.ModifiedFollowing,
            false,
            new Actual360(),
            context),
        Swap(context, 2, 0.031),
        Swap(context, 5, fiveYears),
        Swap(context, 10, 0.033),
    ];

    private static SwapRateHelper Swap(EvaluationContext context, int years, double rate) =>
        new(
            rate,
            new Period(years, TimeUnit.Years),
            new TARGET(),
            Frequency.Annual,
            BusinessDayConvention.Unadjusted,
            new Thirty360(Thirty360Convention.BondBasis),
            new Euribor6M(),
            context);
}
