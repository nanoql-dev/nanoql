// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The arithmetic Monte Carlo engine's geometric control variate (fixed). QuantLib's control path pricer discounts at
/// the last fixing's time (<c>mc_discr_arith_av_price.hpp:175-178</c>) where its analytic control engine discounts at
/// the exercise date, so the controlled price is biased when the exercise follows the last fixing: on spot 100, r 8%, q
/// 0, σ 25% (Act/365 Fixed), a call at 100 on twelve fixings every 30 days prices 7.70961 controlled for 7.86180 ±
/// 0.024 uncontrolled with the exercise 90 days after the last fixing, and 6.810 for 7.402 at 365 days (200,000
/// samples, seed 42). And the control's running product underflows at small spots while its analytic value doesn't: at
/// spot 0.01 with 252 daily fixings the controlled price is +99% to +103% off the uncontrolled one. nanoql discounts
/// the control path at the exercise date and flushes the product into the average where the next factor would underflow
/// it. Checks: the engine without the control within the two estimates' errors, on QuantLib's own pricers and analytic
/// geometric engine; and the controlled price at spot 0.01 against the same contract at spot 100, scaled.
/// </summary>
public class McAsianControlVariateTests
{
    private static readonly Date Today = new(1, Month.January, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly Date[] Monthly = [.. Enumerable.Range(1, 12).Select(i => Today + (30 * i))];

    [Fact]
    public void TheControlMeetsTheUncontrolledPriceNinetyDaysAfterTheLastFixing() => CheckLate(90);

    [Fact]
    public void TheControlMeetsTheUncontrolledPriceAYearAfterTheLastFixing() => CheckLate(365);

    [Fact]
    public void AtASmallSpotTheControlledPriceIsTheScaledOne()
    {
        Date[] daily = [.. Enumerable.Range(1, 252).Select(i => Today + i)];
        double Controlled(double spot)
        {
            var option = new DiscreteAveragingAsianOption(
                AverageType.Arithmetic, 0.0, 0, daily, new PlainVanillaPayoff(OptionType.Call, spot), new EuropeanExercise(daily[^1]));
            return MCDiscreteArithmeticAPEngine.Calculate<PseudoRandom, GeneralStatistics>(
                option, Context, Market(spot, 0.05, 0.20), false, false, true, 20000, null, null, 42).NPV!.Value;
        }

        double small = Controlled(0.01);
        double hundred = Controlled(100.0);
        Assert.Equal(hundred * 1e-4, small, 1e-9 * small);
    }

    private static void CheckLate(int days)
    {
        var option = new DiscreteAveragingAsianOption(
            AverageType.Arithmetic, 0.0, 0, Monthly, new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Monthly[^1] + days));
        GeneralizedBlackScholesProcess p = Market(100.0, 0.08, 0.25);
        OneAssetOptionResults Run(bool control) =>
            MCDiscreteArithmeticAPEngine.Calculate<PseudoRandom, GeneralStatistics>(option, Context, p, false, false, control, 200000, null, null, 42);
        OneAssetOptionResults controlled = Run(true), plain = Run(false);
        double error = Math.Sqrt((controlled.ErrorEstimate!.Value * controlled.ErrorEstimate.Value) + (plain.ErrorEstimate!.Value * plain.ErrorEstimate.Value));
        Assert.True(Math.Abs(controlled.NPV!.Value - plain.NPV!.Value) <= 3.0 * error, $"{controlled.NPV} against {plain.NPV} +- {error}");
    }

    private static GeneralizedBlackScholesProcess Market(double spot, double r, double v) =>
        new BlackScholesMertonProcess(
            spot, new FlatForward(Today, 0.0, A365), new FlatForward(Today, r, A365), new BlackConstantVol(Today, new NullCalendar(), v, A365));
}
