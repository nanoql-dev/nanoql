// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A float-float swap on a schedule of one date. Its leg has no coupon, so each of its vectors is empty, and QuantLib
/// reads the first cap of each leg unchecked (<c>floatfloatswap.cpp:170</c>, <c>:182</c>): undefined behaviour, which
/// on the wheel ends the process (0xC0000005). nanoql refuses the schedule just before the read, with an
/// <see cref="ArgumentException"/> naming it, from either constructor.
/// </summary>
public class FloatFloatSwapOneDateScheduleTests
{
    private static readonly Date Today = new(15, January, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly IborIndex Index = new Euribor6M(new FlatForward(Today, 0.03, new Actual365Fixed()));
    private static readonly Schedule OneDate = new([Today + new Period(1, TimeUnit.Years)], new TARGET());
    private static readonly Schedule TwoYears = new MakeSchedule()
        .From(Today)
        .To(Today + new Period(2, TimeUnit.Years))
        .WithTenor(new Period(6, TimeUnit.Months))
        .WithCalendar(new TARGET())
        .ToSchedule();

    [Fact]
    public void TheConstantTermsConstructorRefusesIt()
    {
        ArgumentException e = Assert.Throws<ArgumentException>(() => new FloatFloatSwap(
            SwapType.Payer, 100.0, 100.0, OneDate, Index, null, TwoYears, Index, null, Context));
        Assert.Equal("schedule1", e.ParamName);
        Assert.StartsWith("schedule1 has a single date, so its leg has no coupons", e.Message, StringComparison.Ordinal);

        // the second leg's, read after the first leg's caps (QuantLib's :182)
        e = Assert.Throws<ArgumentException>(() => new FloatFloatSwap(
            SwapType.Payer, 100.0, 100.0, TwoYears, Index, null, OneDate, Index, null, Context));
        Assert.Equal("schedule2", e.ParamName);
    }

    [Fact]
    public void ThePerCouponConstructorRefusesIt()
    {
        double[] four = [100.0, 100.0, 100.0, 100.0];
        ArgumentException e = Assert.Throws<ArgumentException>(() => new FloatFloatSwap(
            SwapType.Receiver, [], four, OneDate, Index, null, TwoYears, Index, null, Context));
        Assert.Equal("schedule1", e.ParamName);

        e = Assert.Throws<ArgumentException>(() => new FloatFloatSwap(
            SwapType.Receiver, four, [], TwoYears, Index, null, OneDate, Index, null, Context));
        Assert.Equal("schedule2", e.ParamName);
        Assert.StartsWith("schedule2 has a single date, so its leg has no coupons", e.Message, StringComparison.Ordinal);
    }
}
