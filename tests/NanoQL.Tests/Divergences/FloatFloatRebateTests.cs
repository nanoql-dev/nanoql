// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The float-float engine's rebate (the S-g1d plan's G1R6 (v)): QuantLib values it at y = 0 on the model curve, where
/// the nonstandard engine values it on the grid from its payment date. On a Bermudan payer exercised yearly from a year
/// out into a 5-year swap of 1,000,000, its first leg fixed at 3% in all but name (a zero gearing on Euribor 1Y,
/// 30/360) against Euribor 6M, on GSR (a = 0.01, σ = 0.01) on a flat 2.7%, with rebates of 10,000, −5,000, 25,000 and 0
/// paid 252 TARGET days after each exercise, the rebates add 8481.523651884276 on the wheel's float-float engine and
/// 8226.793645308986 on its nonstandard engine for the same swap and exercise, 3.1% apart. nanoql's float-float engine
/// values the rebate as the nonstandard engine does; the check is that engine, the two engines' rebates within 1e-4.
/// </summary>
public class FloatFloatRebateTests
{
    [Fact]
    public void TheRebateIsValuedAsTheNonstandardEngineValuesIt()
    {
        var today = new Date(15, Month.January, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        YieldTermStructure curve = new FlatForward(today, 0.027, new Actual365Fixed());
        var model = new Gsr(curve, [], [0.01], 0.01);
        var cal = new TARGET();
        Date firstExercise = cal.Advance(today, 1, TimeUnit.Years);
        Date start = cal.Advance(firstExercise, 2, TimeUnit.Days);
        Date end = cal.Advance(start, 5, TimeUnit.Years);
        Schedule Schedule(int months) => new(
            start,
            end,
            new Period(months, TimeUnit.Months),
            cal,
            BusinessDayConvention.ModifiedFollowing,
            BusinessDayConvention.ModifiedFollowing,
            DateGenerationRule.Backward,
            false,
            context);
        Schedule fixedSchedule = Schedule(12), floatingSchedule = Schedule(6);
        int n1 = fixedSchedule.Size - 1, n2 = floatingSchedule.Size - 1;
        var dc = new Thirty360(Thirty360Convention.BondBasis);
        var floatFloat = new FloatFloatSwap(
            SwapType.Payer,
            Enumerable.Repeat(1e6, n1),
            Enumerable.Repeat(1e6, n2),
            fixedSchedule,
            new Euribor1Y(curve),
            dc,
            floatingSchedule,
            new Euribor6M(curve),
            new Actual360(),
            context,
            gearing1: new double[n1],
            spread1: Enumerable.Repeat(0.03, n1),
            paymentConvention1: BusinessDayConvention.Following,
            paymentConvention2: BusinessDayConvention.Following);
        var nonstandard = new NonstandardSwap(
            SwapType.Payer,
            Enumerable.Repeat(1e6, n1),
            Enumerable.Repeat(1e6, n2),
            fixedSchedule,
            Enumerable.Repeat(0.03, n1),
            dc,
            floatingSchedule,
            new Euribor6M(curve),
            Enumerable.Repeat(1.0, n2),
            Enumerable.Repeat(0.0, n2),
            new Actual360(),
            context);
        Date[] dates = [.. Enumerable.Range(0, 4).Select(i => cal.Advance(firstExercise, i, TimeUnit.Years))];
        Exercise Exercise(bool rebated) => rebated
            ? new RebatedExercise(new BermudanExercise(dates), [10000.0, -5000.0, 25000.0, 0.0], 252, cal, BusinessDayConvention.Following)
            : new BermudanExercise(dates);
        double FloatFloat(bool rebated) =>
            Gaussian1dFloatFloatSwaptionEngine.Calculate(new FloatFloatSwaption(floatFloat, Exercise(rebated)), context, model, discountCurve: curve).NPV!.Value;
        double Nonstandard(bool rebated) =>
            Gaussian1dNonstandardSwaptionEngine.Calculate(new NonstandardSwaption(nonstandard, Exercise(rebated)), context, model, discountCurve: curve).NPV!.Value;

        double floatFloatRebate = FloatFloat(true) - FloatFloat(false);
        double nonstandardRebate = Nonstandard(true) - Nonstandard(false);

        Assert.Equal(1.0, floatFloatRebate / nonstandardRebate, 1e-4);
    }
}
