// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The float-float swap's refusal of a second leg's nominals that don't match its schedule (the S-rx plan's SRXR3
/// (xix)). QuantLib's check names "schedule2 size" and prints the nominals' size in its place
/// (<c>floatfloatswap.cpp:119-122</c>): three nominals on a schedule of five dates read "nominal2 size (3) does not
/// match schedule2 size (3)". nanoql prints the schedule's size, as QuantLib's first leg's check two lines up does, its
/// wording kept.
/// </summary>
public class FloatFloatSwapRefusalTextTests
{
    [Fact]
    public void TheSecondLegsCheckPrintsTheSchedulesSize()
    {
        var today = new Date(15, January, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        var index = new Euribor6M(new FlatForward(today, 0.03, new Actual365Fixed()));
        Schedule schedule = new MakeSchedule()
            .From(today)
            .To(today + new Period(2, TimeUnit.Years))
            .WithTenor(new Period(6, TimeUnit.Months))
            .WithCalendar(new TARGET())
            .ToSchedule();
        Assert.Equal(5, schedule.Size);

        ArgumentException e = Assert.Throws<ArgumentException>(() => new FloatFloatSwap(
            SwapType.Payer, [1.0, 1.0, 1.0, 1.0], [1.0, 1.0, 1.0], schedule, index, null, schedule, index, null, context));
        Assert.Equal("nominal2", e.ParamName);
        Assert.StartsWith("nominal2 size (3) does not match schedule2 size (5)", e.Message, StringComparison.Ordinal);
    }
}
