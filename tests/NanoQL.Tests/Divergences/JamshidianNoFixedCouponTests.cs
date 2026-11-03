// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Gaussian1d Jamshidian engine with no fixed coupon after the exercise: QuantLib reads
/// <c>fixedResetDates[startIndex]</c> past the vector's end as the coupon bond's value date
/// (<c>gaussian1djamshidianswaptionengine.cpp:84-87</c>, <c>:104</c>, <c>:107</c>, <c>:139</c>, <c>:159</c>,
/// <c>:163</c>), undefined behaviour: on the wheel, a 2-year payer at 3% (annual fixed and 6-month floating coupons) on
/// a GSR model (a = 0.02, σ = 0.01) on a flat 3% curve, exercised 13 months out, throws "negative time (-3.73411e+06)
/// given", and a different refusal each run. nanoql takes the first floating reset on or after the exercise as the
/// value date at every one of those reads, the start of the coupons QuantLib's Gaussian1d swaption engine exercises
/// into, which values the swaption at 0.014428105830926762 (13 months) and 0.014437260795231834 (18 months); with no
/// coupon after the exercise (20 months), the swaption is worth 0, that engine's convention. QuantLib's check: its
/// Gaussian1d swaption engine on the same swaption and model, to the two engines' integration error.
/// </summary>
public class JamshidianNoFixedCouponTests
{
    private static readonly Date Today = new(15, Month.January, 2025);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly YieldTermStructure Curve = new FlatForward(Today, 0.03, new Actual365Fixed());
    private static readonly Gsr Model = Gaussian1dInputs.Constant(Curve, 0.02, 0.01);

    [Fact]
    public void ASwaptionWithNoFixedCouponAfterItsExerciseIsPricedOnItsFloatingResets()
    {
        foreach ((int months, double quantLibSwaptionEngine) in new[] { (13, 0.014428105830926762), (18, 0.014437260795231834) })
        {
            Swaption swaption = Swaption(months);
            double jamshidian = Gaussian1dJamshidianSwaptionEngine.Calculate(swaption, Context, Model).NPV!.Value;
            double engine = Gaussian1dSwaptionEngine.Calculate(swaption, Context, Model).NPV!.Value;
            Assert.True(Math.Abs((engine / quantLibSwaptionEngine) - 1.0) <= 1e-13, $"{months}: {engine}");
            Assert.True(Math.Abs((jamshidian / engine) - 1.0) <= 1e-9, $"{months}: {jamshidian} against {engine}");
        }
    }

    [Fact]
    public void WithNoCouponAfterItsExerciseTheSwaptionIsWorthNothing()
    {
        Assert.Equal(0.0, Gaussian1dJamshidianSwaptionEngine.Calculate(Swaption(20), Context, Model).NPV);
        Assert.Equal(0.0, Gaussian1dSwaptionEngine.Calculate(Swaption(20), Context, Model).NPV);
    }

    private static Swaption Swaption(int months)
    {
        var calendar = new TARGET();
        Date start = calendar.Advance(Today, 2, TimeUnit.Days);
        VanillaSwap swap = Gaussian1dInputs.SwapOf(new Euribor6M(Curve), Today, 2, 0.03, SwapType.Payer, Context, start: start);
        return new Swaption(swap, new EuropeanExercise(calendar.Advance(Today, months, TimeUnit.Months)));
    }
}
