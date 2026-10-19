// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// A null BMA index is refused on entry, with QuantLib's text for a missing index, "no index provided"
/// (<c>floatingratecoupon.cpp:52</c>), naming the parameter. QuantLib's <c>AverageBMACoupon</c> reads its index's
/// fixing days before its base checks the index (<c>averagebmacoupon.cpp:111-113</c>), <c>AverageBMALeg</c> never
/// checks it (<c>:165-166</c>) and its coupons then read it, and <c>BMASwap</c> passes it to its BMA leg
/// (<c>bmaswap.cpp:55-58</c>): results C++ leaves undefined, where the wheel stops on Boost's assertion.
/// </summary>
public class BMANullIndexTests
{
    [Fact]
    public void ANullBMAIndexIsRefusedWithQuantLibsText()
    {
        var today = new Date(15, Month.January, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        Schedule schedule = new MakeSchedule().From(new Date(17, Month.January, 2025))
            .To(new Date(17, Month.January, 2026)).WithTenor(new Period(3, TimeUnit.Months))
            .WithCalendar(new TARGET()).ToSchedule();

        AssertRefused("index", () => new AverageBMACoupon(
            new Date(17, Month.April, 2025),
            1e6,
            new Date(17, Month.January, 2025),
            new Date(17, Month.April, 2025),
            null!));
        AssertRefused("index", () => new AverageBMALeg(schedule, null!));
        AssertRefused("bmaIndex", () => new BMASwap(
            SwapType.Payer,
            1e6,
            schedule,
            0.7,
            0.0,
            new Euribor3M(),
            null,
            schedule,
            null!,
            null,
            context));
    }

    private static void AssertRefused(string paramName, Func<object> build)
    {
        ArgumentNullException e = Assert.Throws<ArgumentNullException>(build);
        Assert.Equal(paramName, e.ParamName);
        Assert.StartsWith("no index provided", e.Message, StringComparison.Ordinal);
    }
}
