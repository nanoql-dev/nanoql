// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The floating leg builder given a null index: QuantLib's <c>FloatingLeg</c> reads <c>index-&gt;fixingDays()</c> as
/// a default argument (<c>cashflowvectors.hpp:156, 169, 188, 204</c>), a null pointer's dereference, which is
/// undefined. nanoql refuses it with <see cref="ArgumentNullException"/> naming <c>index</c>. The empty schedule, which
/// QuantLib refuses with <c>std::length_error</c>, is a convention (<c>Conventions/EmptyScheduleTests</c>).
/// </summary>
public class FloatingLegUndefinedInputsTests
{
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
