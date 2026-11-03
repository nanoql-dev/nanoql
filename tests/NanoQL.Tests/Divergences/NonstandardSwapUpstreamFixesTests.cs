// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The fix QuantLib's master made to the nonstandard swap's conversion from a vanilla or overnight-indexed swap after
/// the pin, which nanoql takes: #2956 (<c>a069a0955</c>), each leg's own nominals, the last one repeated to the leg's
/// length. QuantLib 1.44 repeats the swap's <c>nominal()</c> on every coupon (<c>nonstandardswap.cpp:39-44</c>),
/// which refuses an amortizing swap, "nominal is not constant".
/// </summary>
public class NonstandardSwapUpstreamFixesTests
{
    [Fact]
    public void TestNonstandardCopyOfAmortisingOvernightSwap()
    {
        // Upstream: QuantLib's master's case for #2956 (gsr.cpp's testNonstandardCopyOfAmortisingOvernightSwap)
        var today = new Date(25, Month.June, 2026);
        var context = new EvaluationContext { EvaluationDate = today };
        var curve = new FlatForward(today, 0.037123, new Actual365Fixed());
        var index = new Sofr(curve);
        Calendar calendar = index.FixingCalendar;
        var schedule = new Schedule(
            new Date(28, Month.June, 2027),
            new Date(28, Month.June, 2030),
            new Period(1, TimeUnit.Years),
            calendar,
            BusinessDayConvention.ModifiedFollowing,
            BusinessDayConvention.ModifiedFollowing,
            DateGenerationRule.Forward,
            false,
            context);

        var original = new OvernightIndexedSwap(
            SwapType.Payer,
            [1000000, 800000],
            schedule,
            0.035,
            new Actual360(),
            index,
            0.001,
            3,
            BusinessDayConvention.Preceding,
            new TARGET(),
            false,
            RateAveragingType.Compound,
            2,
            2,
            true);
        var copy = new NonstandardSwap(original, context);
        Assert.Equal(original.PaymentLag, copy.PaymentLag);
        Assert.Equal(original.PaymentConvention, copy.PaymentConvention);
        for (int leg = 0; leg < 2; ++leg)
        {
            var source = original.Leg(leg);
            var target = copy.Leg(leg);
            Assert.Equal(source.Length, target.Length);
            for (int i = 0; i < source.Length; ++i)
            {
                var expected = Assert.IsAssignableFrom<Coupon>(source[i]);
                var actual = Assert.IsAssignableFrom<Coupon>(target[i]);
                Assert.Equal(expected.Nominal(context), actual.Nominal(context));
                Assert.Equal(expected.Date, actual.Date);
                Assert.True(Math.Abs(actual.Amount(context) - expected.Amount(context)) < 1.0e-8);
            }
        }
    }
}
