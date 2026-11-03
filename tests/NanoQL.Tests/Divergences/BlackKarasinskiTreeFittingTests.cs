// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// Black-Karasinski's trees on one model: QuantLib's <c>BlackKarasinski::tree</c> writes the model's one fitting
/// parameter <c>phi_</c> (<c>blackkarasinski.cpp:64</c>, <c>:79-81</c>), so building a second tree on another grid
/// rewrites the first's fitting: on the wheel, a grid engine's swaption, exercised on its swap's start date, which the
/// discretized swaption's snapping leaves alone, prices 0.007389451726576681 alone and 0.007389042124133561 once a
/// second grid engine has built its tree on the same model. nanoql's tree owns its fitting, as the Hull-White tree
/// does, so a lattice prices the same whatever is built after it. QuantLib's check: a fresh model's tree on the first
/// grid, 0.007389451726576681.
/// </summary>
public class BlackKarasinskiTreeFittingTests
{
    [Fact]
    public void ATreeKeepsItsFittingWhenAnotherIsBuilt()
    {
        var today = new Date(15, Month.January, 2025);
        var context = new EvaluationContext { EvaluationDate = today };
        var curve = new FlatForward(today, 0.04, new Actual365Fixed());
        var model = new BlackKarasinski(curve, 0.1, 0.1);
        (Swaption swaption, double[] times) = Swaption(today, curve, context);

        Lattice first = model.Tree(new TimeGrid(times, 40));
        double alone = TreeSwaptionEngine.Calculate(swaption, context, model, first).NPV!.Value;
        Lattice second = model.Tree(new TimeGrid(times, 70));
        double after = TreeSwaptionEngine.Calculate(swaption, context, model, first).NPV!.Value;

        Assert.Equal(alone, after);
        Assert.True(Tolerance.Within(0.007389451726576681, after), $"{after}");
        Assert.NotEqual(after, TreeSwaptionEngine.Calculate(swaption, context, model, second).NPV!.Value);
    }

    // A 1Y into 5Y payer at 4% on Euribor 6M, notional 1, exercised on its swap's start, and the year fractions on the
    // curve of its exercise and its coupons' starts and payments.
    private static (Swaption Swaption, double[] Times) Swaption(Date today, YieldTermStructure curve, EvaluationContext context)
    {
        var calendar = new TARGET();
        Date start = calendar.Advance(calendar.Advance(today, new Period(1, TimeUnit.Years)), 2, TimeUnit.Days);
        Date exercise = start;
        var fixedSchedule = new Schedule(
            start, start + new Period(5, TimeUnit.Years), new Period(1, TimeUnit.Years), calendar, BusinessDayConvention.ModifiedFollowing,
            BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Forward, false, context);
        var floatSchedule = new Schedule(
            start, start + new Period(5, TimeUnit.Years), new Period(6, TimeUnit.Months), calendar, BusinessDayConvention.ModifiedFollowing,
            BusinessDayConvention.ModifiedFollowing, DateGenerationRule.Forward, false, context);
        var swap = new VanillaSwap(
            SwapType.Payer, 1.0, fixedSchedule, 0.04, new Thirty360(Thirty360Convention.BondBasis), floatSchedule,
            new Euribor6M(curve), 0.0, new Actual360(), context);
        var dc = new Actual365Fixed();
        var times = new List<double> { dc.YearFraction(today, exercise) };
        foreach (CashFlow cf in swap.FixedLeg.AddRange(swap.FloatingLeg))
        {
            var coupon = (Coupon)cf;
            times.Add(dc.YearFraction(today, coupon.AccrualStartDate));
            times.Add(dc.YearFraction(today, coupon.Date));
        }

        return (new Swaption(swap, new EuropeanExercise(exercise)), [.. times]);
    }
}
