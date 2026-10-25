// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The FD Heston-Hull-White engine where the carry is large against the volatility, or a strike far from the forwards (D3-1
/// of the S-hes run record, the FD Heston engine's B1-1 swept). QuantLib sizes the equity mesher on a helper whose curves
/// its call binds in each other's places (<c>fdhestonhullwhitevanillaengine.cpp:115-131</c>), so the grid walks the
/// forward's mirror: a five-year call at 1.25 times the forward, carry 0.30 and volatility 0.1, priced 0 where the analytic
/// Heston-Hull-White engine gives 1.7595, on the default grid and on twice its points; and a year's put at 0.7 of the
/// forward, carry −0.2, priced 0 for 0.0068, its strike beyond QuantLib's grid [executed: <c>scratchpad\c3\oracle_c3b.py</c>].
/// nanoql's mesher is QuantLib's wherever its range holds the true forwards' band and the strike's, at half the mesher's
/// margin, and is widened to the bands where it doesn't; its multi-strike solve solves each strike alone where its grid
/// doesn't hold the forwards' band at its read points. Checked against the analytic engine, at no equity-rate
/// correlation, under refinement.
/// </summary>
public class FdHestonHullWhiteHighCarryTests
{
    private static readonly Date Today = new(15, October, 2026);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    [Fact]
    public void AtHighCarryTheCallConvergesToTheAnalyticEngine()
    {
        (HestonModel model, HullWhiteProcess hullWhite, double strike) = Market(0.31, 0.01, 1826, 1.25);
        var call = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, strike), new EuropeanExercise(Today + 1826));
        AssertConverges(call, model, hullWhite, 1.7595114699771237, 0.25, 0.06); // QuantLib 0 on both grids
    }

    [Fact]
    public void AStrikeBeyondTheForwardsBandKeepsItsTail()
    {
        (HestonModel model, HullWhiteProcess hullWhite, double strike) = Market(0.01, 0.21, 365, 0.7);
        var put = new VanillaOption(new PlainVanillaPayoff(OptionType.Put, strike), new EuropeanExercise(Today + 365));
        AssertConverges(put, model, hullWhite, 0.0068488618190565564, 0.1, 0.03); // QuantLib 0 on both grids
    }

    [Fact]
    public void AtHighCarryTheMultiStrikeSolveSolvesEachStrikeAlone()
    {
        (HestonModel model, HullWhiteProcess hullWhite, double strike) = Market(0.31, 0.01, 1826, 1.0);
        var exercise = new EuropeanExercise(Today + 1826);
        double[] strikes = [0.8 * strike, strike, 1.25 * strike];
        var reference = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, strike), exercise);
        ImmutableArray<OneAssetOptionResults> results = FdHestonHullWhiteVanillaEngine.CalculateStrikes(
            reference, strikes, Context, model, hullWhite, 0.0, 20, 40, 10, 7, 0, false);
        for (int i = 0; i < strikes.Length; ++i)
        {
            var single = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, strikes[i]), exercise);
            Assert.Equal(
                FdHestonHullWhiteVanillaEngine.Calculate(single, Context, model, hullWhite, 0.0, 20, 40, 10, 7, 0, false).NPV,
                results[i].NPV);
        }
    }

    // a Heston process at volatility 0.1 (v0 = theta 0.01, kappa 1.5, sigma 0.1, rho -0.5), the Hull-White process 0.1,
    // 0.01 on the risk-free curve, and the strike at a multiple of the forward to the maturity
    private static (HestonModel Model, HullWhiteProcess HullWhite, double Strike) Market(double r, double q, int days, double k)
    {
        YieldTermStructure riskFree = new FlatForward(Today, r, A365), dividends = new FlatForward(Today, q, A365);
        var model = new HestonModel(new HestonProcess(riskFree, dividends, 100.0, 0.01, 1.5, 0.01, 0.1, -0.5));
        double forward = 100.0 * dividends.Discount(Today + days) / riskFree.Discount(Today + days);
        return (model, new HullWhiteProcess(riskFree, 0.1, 0.01), k * forward);
    }

    // the default grid and twice its time steps and log-spot points, without the control variate: within the first and
    // second errors of the analytic engine, the second below a third of the first
    private static void AssertConverges(
        VanillaOption option, HestonModel model, HullWhiteProcess hullWhite, double analytic, double first, double second)
    {
        double coarse = FdHestonHullWhiteVanillaEngine.Calculate(option, Context, model, hullWhite, 0.0, 50, 100, 40, 20, 0, false).NPV!.Value;
        double fine = FdHestonHullWhiteVanillaEngine.Calculate(option, Context, model, hullWhite, 0.0, 100, 200, 40, 20, 0, false).NPV!.Value;
        double e1 = Math.Abs(coarse - analytic) / analytic, e2 = Math.Abs(fine - analytic) / analytic;
        Assert.True(e1 <= first && e2 <= second && e2 < e1 / 3, $"errors {e1} and {e2} ({coarse}, {fine} for {analytic})");
    }
}
