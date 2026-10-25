// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The Monte Carlo Heston engine on curves dated or counted differently (the S-hes plan's HR7 (vi), the mismatched-dates
/// class of master Revision 58). QuantLib's engine runs its paths on the process's times, the risk-free curve's from its
/// reference date, reads the dividend curve at those times and discounts at the grid's last time on the risk-free curve
/// (<c>mceuropeanhestonengine.hpp:121-129</c>). nanoql's runs its paths on the model's clock, from the earliest curve
/// date on the risk-free curve's day counter, each curve read at the instants the grid's times denote, and discounts
/// there. QuantLib's check: its engine on the market restated on one clock, from the same seed, which the fixed engine on
/// the original market reproduces to rounding [executed on the wheel: <c>scratchpad\c1\oracle_c1.py</c>].
/// </summary>
public class McHestonMismatchedTimesTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    private static readonly VanillaOption Call =
        new(new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(Today + 365));

    [Fact]
    public void ADividendCurveOnAnotherDayCounterIsReadAtItsOwnTimes()
    {
        // q 2% on Act/365 beside r 5% on Act/360; QuantLib's restated market: q 2%·360/365 on Act/360
        OneAssetOptionResults results = Price(new FlatForward(Today, 0.05, new Actual360()), new FlatForward(Today, 0.02, A365));
        AssertClose(8.856651516417413, results.NPV!.Value); // as given 8.838291295560206
        AssertClose(0.10989987120426303, results.ErrorEstimate!.Value);
    }

    [Fact]
    public void ARiskFreeCurveSettlingLaterIsReadFromItsDateOnTheEarliestClock()
    {
        // r 5% settling T+2, q 2% today; QuantLib's restated market: the risk-free discount 1 to T+2 and 5% after
        OneAssetOptionResults results = Price(new FlatForward(Today + 2, 0.05, A365), new FlatForward(Today, 0.02, A365));
        AssertClose(8.755878963326191, results.NPV!.Value); // as given 8.745133281532699
        AssertClose(0.10888354862507442, results.ErrorEstimate!.Value);
    }

    // QE martingale, v0 = theta 0.04, kappa 1.5, sigma 0.4, rho -0.6; 20 steps, 10,000 samples, seed 42
    private static OneAssetOptionResults Price(YieldTermStructure riskFree, YieldTermStructure dividends) =>
        MCEuropeanHestonEngine.Calculate<PseudoRandom, GeneralStatistics>(
            Call,
            Context,
            new HestonProcess(riskFree, dividends, 100.0, 0.04, 1.5, 0.04, 0.4, -0.6),
            20,
            null,
            false,
            10000,
            null,
            null,
            42);

    private static void AssertClose(double expected, double calculated) =>
        Assert.True(
            Math.Abs(calculated - expected) <= 1e-11 * Math.Abs(expected),
            $"expected {expected}, calculated {calculated}");
}
