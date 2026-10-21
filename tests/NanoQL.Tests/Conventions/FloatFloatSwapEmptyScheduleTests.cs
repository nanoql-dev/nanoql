// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// A float-float swap on an empty schedule. QuantLib's constant-terms constructor sizes each term's vector by the
/// schedule's coupon count, <c>schedule.size() - 1</c> (<c>floatfloatswap.cpp:58-69</c>), which an empty schedule wraps
/// to its largest value, and <c>std::vector</c>'s constructor throws <c>std::length_error</c>: a C++ library exception,
/// refused at the same point with "empty schedule", naming the schedule (the playbook's row for such exceptions). The
/// per-coupon constructor's own check refuses it first, with QuantLib's text (<c>:115-118</c>).
/// </summary>
public class FloatFloatSwapEmptyScheduleTests
{
    [Fact]
    public void AnEmptyScheduleIsRefused()
    {
        var today = new Date(15, January, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        var curve = new FlatForward(today, 0.03, new Actual365Fixed());
        var index = new Euribor6M(curve);
        var empty = new Schedule([], new TARGET());
        Schedule schedule = new MakeSchedule()
            .From(today)
            .To(today + new Period(2, TimeUnit.Years))
            .WithTenor(index.Tenor)
            .WithCalendar(new TARGET())
            .ToSchedule();

        ArgumentException e = Assert.Throws<ArgumentException>(() => new FloatFloatSwap(
            SwapType.Payer, 100.0, 100.0, empty, index, null, schedule, index, null, context));
        Assert.Equal("schedule1", e.ParamName);
        Assert.StartsWith("empty schedule", e.Message, StringComparison.Ordinal);

        e = Assert.Throws<ArgumentException>(() => new FloatFloatSwap(
            SwapType.Payer, 100.0, 100.0, schedule, index, null, empty, index, null, context));
        Assert.Equal("schedule2", e.ParamName);

        // the per-coupon constructor: QuantLib's own check
        e = Assert.Throws<ArgumentException>(() => new FloatFloatSwap(
            SwapType.Payer, [100.0], [100.0, 100.0, 100.0, 100.0], empty, index, null, schedule, index, null, context));
        Assert.Equal("nominal1", e.ParamName);
        Assert.StartsWith("nominal1 size (1) does not match schedule1 size (0)", e.Message, StringComparison.Ordinal);
    }
}
