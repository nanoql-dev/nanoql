// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The swap's inner value off its exercise dates: QuantLib's <c>FdmAffineModelSwapInnerValue</c> reads its exercise
/// map's <c>find(t)->second</c> at the step condition's time, and an American exercise's condition reads it at every
/// step of its window, where <c>find</c> returns the map's end (undefined behaviour, the playbook's undefined-behaviour
/// row); nanoql refuses a time not among the exercise times.
/// </summary>
public class InnerValueAmericanRefusalTests
{
    [Fact]
    public void AnAmericanSwaptionIsRefused()
    {
        var today = new Date(15, Month.January, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        var curve = new FlatForward(today, 0.027, new Actual365Fixed());
        var calendar = new TARGET();
        Date exercise = calendar.Advance(today, new Period(1, TimeUnit.Years));
        Date start = calendar.Advance(exercise, 2, TimeUnit.Days);
        var fixedSchedule = new Schedule(
            start, start + new Period(5, TimeUnit.Years), new Period(1, TimeUnit.Years), calendar,
            BusinessDayConvention.ModifiedFollowing, BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Forward, false, context);
        var floatSchedule = new Schedule(
            start, start + new Period(5, TimeUnit.Years), new Period(6, TimeUnit.Months), calendar,
            BusinessDayConvention.ModifiedFollowing, BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Forward, false, context);
        var swap = new VanillaSwap(
            SwapType.Payer, 1e6, fixedSchedule, 0.03, new Thirty360(Thirty360Convention.BondBasis), floatSchedule,
            new Euribor6M(curve), 0.0, new Actual360(), context);
        var swaption = new Swaption(swap, new AmericanExercise(today, exercise));

        NanoQLException e = Assert.Throws<NanoQLException>(
            () => FdHullWhiteSwaptionEngine.Calculate(swaption, context, new HullWhite(curve, 0.05, 0.01), 20, 25));
        Assert.StartsWith("no exercise date at time ", e.Message, StringComparison.Ordinal);
    }
}
