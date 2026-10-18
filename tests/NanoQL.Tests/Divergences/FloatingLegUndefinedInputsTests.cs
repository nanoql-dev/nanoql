// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The floating leg builders' undefined inputs. An empty schedule: QuantLib's <c>FloatingLeg</c> takes
/// <c>schedule.size()-1</c> periods, a <c>Size</c> wrapping below zero, so its size checks all pass and
/// <c>leg.reserve(n)</c> fails (<c>cashflowvectors.hpp:82-102</c>; <c>IborLeg</c> reaches it at
/// <c>iborcoupon.cpp:286</c>). A null index: <c>FloatingLeg</c> reads <c>index-&gt;fixingDays()</c> as a default
/// argument (<c>cashflowvectors.hpp:156, 169, 188, 204</c>). nanoql refuses the empty schedule where
/// <see cref="IborLeg"/> is made and where <see cref="CashFlowVectorsFunctions.FloatingLeg"/> is called, with
/// <see cref="ArgumentException"/>, "empty schedule", and the null index with <see cref="ArgumentNullException"/>.
/// </summary>
public class FloatingLegUndefinedInputsTests
{
    [Fact]
    public void AnEmptyScheduleIsRefusedByBothBuilders()
    {
        var empty = new Schedule([], new TARGET());

        ArgumentException e = Assert.Throws<ArgumentException>(() => new IborLeg(empty, new Euribor6M()));
        Assert.Equal("schedule", e.ParamName);
        Assert.StartsWith("empty schedule", e.Message, StringComparison.Ordinal);

        e = Assert.Throws<ArgumentException>(() => FloatingLeg(empty, new Euribor6M()));
        Assert.Equal("schedule", e.ParamName);
        Assert.StartsWith("empty schedule", e.Message, StringComparison.Ordinal);

        // A one-date schedule stays QuantLib's: no periods, so the nominal given is one too many.
        var oneDate = new Schedule([new Date(15, Month.January, 2025)], new TARGET());
        e = Assert.Throws<ArgumentException>(() => FloatingLeg(oneDate, new Euribor6M()));
        Assert.Equal("nominals", e.ParamName);
        Assert.StartsWith("too many nominals (1), only 0 required", e.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ANullIndexIsRefused()
    {
        var schedule = new Schedule([new Date(15, Month.January, 2025), new Date(15, Month.July, 2025)], new TARGET());

        Assert.Equal("index", Assert.Throws<ArgumentNullException>(() => FloatingLeg(schedule, null!)).ParamName);
    }

    private static ImmutableArray<CashFlow> FloatingLeg(Schedule schedule, IborIndex index) =>
        CashFlowVectorsFunctions.FloatingLeg<IborIndex>(
            (paymentDate, nominal, start, end, fixingDays, i, gearing, spread, refStart, refEnd, dc, inArrears, exCoupon, fixingConvention) =>
                new IborCoupon(paymentDate, nominal, start, end, fixingDays, i, gearing, spread, refStart, refEnd, dc, inArrears, exCoupon, fixingConvention),
            (paymentDate, nominal, start, end, fixingDays, i, gearing, spread, cap, floor, refStart, refEnd, dc, inArrears, exCoupon, fixingConvention) =>
                new CappedFlooredIborCoupon(paymentDate, nominal, start, end, fixingDays, i, gearing, spread, cap, floor, refStart, refEnd, dc, inArrears, exCoupon, fixingConvention),
            schedule,
            [1e6],
            index,
            null,
            BusinessDayConvention.Following,
            [],
            [],
            [],
            [],
            [],
            false,
            false);
}
