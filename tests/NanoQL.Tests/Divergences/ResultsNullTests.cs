// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A result QuantLib leaves null is <see langword="null"/> in nanoql's results records. QuantLib's accessors throw on
/// reading such a result: <c>NPV()</c> "NPV not provided", <c>errorEstimate()</c> "error estimate not provided",
/// <c>valuationDate()</c> "valuation date not provided", and the swap's per-leg accessors "result not available"
/// (<c>instrument.hpp</c>, <c>swap.hpp</c>).
/// </summary>
public class ResultsNullTests
{
    private static readonly Date Today = new(15, Month.October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly FlatForward Curve = new(Today, 0.03, new Actual365Fixed());

    [Fact]
    public void UnsetResultsAreNull()
    {
        // QuantLib's NPV(), errorEstimate() and valuationDate() throw on these.
        var results = new InstrumentResults();
        Assert.Null(results.NPV);
        Assert.Null(results.ErrorEstimate);
        Assert.Null(results.ValuationDate);
        Assert.Empty(results.AdditionalResults);

        var swapResults = new SwapResults();
        Assert.Null(swapResults.NpvDateDiscount);
        Assert.Empty(swapResults.LegNPV);
    }

    [Fact]
    public void TheDiscountingEngineLeavesTheErrorEstimateNull()
    {
        var swap = new Swap(
            Leg(new Date(15, Month.January, 2027), new Date(15, Month.January, 2032), 0.03),
            Leg(new Date(15, Month.January, 2027), new Date(15, Month.January, 2032), 0.025));

        SwapResults results = DiscountingSwapEngine.Calculate(swap, Context, Curve);

        Assert.NotNull(results.NPV);
        Assert.Null(results.ErrorEstimate);
        Assert.Equal(Today, results.ValuationDate);
    }

    [Fact]
    public void ALegStartingBeforeTheCurveHasNoStartDiscount()
    {
        var swap = new Swap(
            Leg(new Date(15, Month.January, 2026), new Date(15, Month.January, 2031), 0.03),
            Leg(new Date(15, Month.January, 2027), new Date(15, Month.January, 2031), 0.025));

        SwapResults results = DiscountingSwapEngine.Calculate(swap, Context, Curve);

        Assert.Null(results.StartDiscounts[0]);
        Assert.NotNull(results.StartDiscounts[1]);
        Assert.NotNull(results.EndDiscounts[0]);
        Assert.NotNull(results.EndDiscounts[1]);
    }

    [Fact]
    public void AnEmptyLegHasNoStartOrEndDiscount()
    {
        var swap = new Swap(Leg(new Date(15, Month.January, 2027), new Date(15, Month.January, 2032), 0.03), []);

        SwapResults results = DiscountingSwapEngine.Calculate(swap, Context, Curve);

        Assert.Null(results.StartDiscounts[1]);
        Assert.Null(results.EndDiscounts[1]);
        Assert.Equal(0.0, results.LegNPV[1]);
    }

    [Fact]
    public void AnExpiredSwapHasNoValuationDate()
    {
        var swap = new Swap(
            Leg(new Date(15, Month.January, 2020), new Date(15, Month.January, 2024), 0.03),
            Leg(new Date(15, Month.January, 2020), new Date(15, Month.January, 2024), 0.025));

        SwapResults results = DiscountingSwapEngine.Calculate(swap, Context, Curve);

        Assert.Null(results.ValuationDate);
        Assert.Equal(0.0, results.NPV);
        Assert.Equal(0.0, results.ErrorEstimate);
        Assert.Equal(new double?[] { 0.0, 0.0 }, results.LegNPV);
        Assert.Equal(0.0, results.NpvDateDiscount);
    }

    [Fact]
    public void PerLegResultsAnEngineDoesNotReturnAreNull()
    {
        // Swap::fetchResults fills a per-leg result the engine leaves empty with Null<Real>().
        var swap = new Swap(Leg(new Date(15, Month.January, 2027), new Date(15, Month.January, 2032), 0.03), []);

        SwapResults results = swap.FetchResults(new SwapResults { NPV = 1.0 });

        Assert.Equal(new double?[] { null, null }, results.LegNPV);
        Assert.Equal(new double?[] { null, null }, results.LegBPS);
        Assert.Equal(new double?[] { null, null }, results.StartDiscounts);
        Assert.Equal(new double?[] { null, null }, results.EndDiscounts);
        Assert.Null(results.NpvDateDiscount);
        Assert.Equal(1.0, results.NPV);
    }

    private static ImmutableArray<CashFlow> Leg(Date start, Date end, double rate) =>
        new FixedRateLeg(new MakeSchedule()
                .From(start)
                .To(end)
                .WithFrequency(Frequency.Semiannual)
                .WithCalendar(new TARGET())
                .ToSchedule())
            .WithNotionals(1e6)
            .WithCouponRates(rate, new Actual360())
            .ToLeg();
}
