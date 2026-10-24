// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A compounded overnight-index future on its value date (the S-cms plan's CMR5 (xxi)): QuantLib's
/// <c>compoundedRate</c> compounds the past fixings strictly before the evaluation date and forecasts from it, so on
/// the value date today's published fixing is ignored, where the averaged future reads it
/// (<c>overnightindexfuture.cpp</c>). On the wheel, a SOFR future from 23 October 2026 to 25 January 2027 on a flat 5%
/// Actual/360 curve prices 94.96721860668744 compounded with or without a 7% fixing for today, where averaged it prices
/// 94.99926113524772 without and 94.93546459731232 with it. nanoql's compounded future uses today's fixing once
/// published. QuantLib's check: the same future the next business day (26 October), the value date's fixing then
/// past, 94.90261063438486 compounded and 94.93546459731232 averaged.
/// </summary>
public class OvernightFutureTodayFixingTests
{
    private static readonly Date Today = new(23, Month.October, 2026);
    private static readonly Sofr Index = new(new FlatForward(Today, 0.05, new Actual360()));

    [Fact]
    public void TodaysFixingIsUsedOnTheValueDate()
    {
        var context = new EvaluationContext { EvaluationDate = Today };
        context = context with { Fixings = context.Fixings.AddFixing(Index, Today, 0.07) };
        Assert.True(Math.Abs(Future(RateAveragingType.Compound).Calculate(context).NPV!.Value - 94.90261063438486) <= 1e-12);
        Assert.True(Math.Abs(Future(RateAveragingType.Simple).Calculate(context).NPV!.Value - 94.93546459731232) <= 1e-12);

        var unpublished = new EvaluationContext { EvaluationDate = Today };
        Assert.True(Math.Abs(Future(RateAveragingType.Compound).Calculate(unpublished).NPV!.Value - 94.96721860668744) <= 1e-12);
        Assert.True(Math.Abs(Future(RateAveragingType.Simple).Calculate(unpublished).NPV!.Value - 94.99926113524772) <= 1e-12);
    }

    private static OvernightIndexFuture Future(RateAveragingType averaging) =>
        new(Index, Today, Index.FixingCalendar.Advance(Today, new Period(3, TimeUnit.Months)), null, averaging);
}
