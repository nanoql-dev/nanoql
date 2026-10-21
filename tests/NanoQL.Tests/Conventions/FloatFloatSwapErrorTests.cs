// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The float-float swap's refusals the vectors don't reach, and its CMS leg's: each refusal's type, text and parameter
/// (the playbook's §9.4). QuantLib's type is any value, an unknown one reading as a receiver; its index checks
/// (<c>floatfloatswap.cpp:229-245</c>) refuse a null index and one that is neither Ibor nor swap with one text each;
/// <c>CmsLeg</c> refuses a null index, "no index provided" (<c>cmscoupon.cpp:60</c>).
/// </summary>
public class FloatFloatSwapErrorTests
{
    private static readonly Date Today = new(15, January, 2025);

    [Fact]
    public void AnUnknownTypeIsRefused()
    {
        EvaluationContext context = Context();
        var index = new Euribor6M(Curve());
        Schedule schedule = Annual();
        ArgumentOutOfRangeException e = Assert.Throws<ArgumentOutOfRangeException>(() => new FloatFloatSwap(
            (SwapType)0, 1.0, 1.0, schedule, index, null, schedule, index, null, context));
        Assert.Equal("type", e.ParamName);
    }

    [Fact]
    public void ANullIndexAndAnIndexNeitherIborNorSwapAreRefused()
    {
        EvaluationContext context = Context();
        var index = new Euribor6M(Curve());
        Schedule schedule = Annual();

        ArgumentNullException nullIndex = Assert.Throws<ArgumentNullException>(() => new FloatFloatSwap(
            SwapType.Payer, 1.0, 1.0, schedule, null!, null, schedule, index, null, context));
        Assert.Equal("index1", nullIndex.ParamName);
        Assert.StartsWith("index1 must be ibor or cms or cms spread", nullIndex.Message, StringComparison.Ordinal);

        ArgumentException bma = Assert.Throws<ArgumentException>(() => new FloatFloatSwap(
            SwapType.Payer, 1.0, 1.0, schedule, index, null, schedule, new BMAIndex(), null, context));
        Assert.Equal("index2", bma.ParamName);
        Assert.StartsWith("index2 must be ibor or cms", bma.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void TheCmsLegRefusesANullIndex()
    {
        ArgumentNullException e = Assert.Throws<ArgumentNullException>(() => new CmsLeg(Annual(), null!));
        Assert.Equal("swapIndex", e.ParamName);
        Assert.StartsWith("no index provided", e.Message, StringComparison.Ordinal);
    }

    private static EvaluationContext Context() => new() { EvaluationDate = Today };

    private static FlatForward Curve() => new(Today, 0.03, new Actual365Fixed());

    private static Schedule Annual() =>
        new MakeSchedule()
            .From(Today)
            .To(Today + new Period(2, TimeUnit.Years))
            .WithTenor(new Period(1, TimeUnit.Years))
            .WithCalendar(new TARGET())
            .ToSchedule();
}
