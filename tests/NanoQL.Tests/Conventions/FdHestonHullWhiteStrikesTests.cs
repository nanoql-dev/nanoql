// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// QuantLib's multiple-strikes caching of the finite-difference Heston-Hull-White engine as a call:
/// <see cref="FdHestonHullWhiteVanillaEngine.CalculateStrikes(VanillaOption, IEnumerable{double}, EvaluationContext, HestonModel, HullWhiteProcess, double, int, int, int, int, int, bool, FdmSchemeDesc?)"/>
/// solves once on QuantLib's multi-strike mesher and returns each strike's results in the strikes' order: the value
/// V(S·d)/d, delta, gamma·d and theta/d at d = K/Kᵢ, bit for bit the solver's on the problem QuantLib's engine builds
/// with the strikes, the delta and gamma at a shift of 1% of S·d; with the control variate each value corrected by the
/// analytic Heston engine's price at Kᵢ less the finite-difference Heston engine's multiple-strikes value; and
/// QuantLib's refusals, of discrete dividends and of no strikes.
/// </summary>
public class FdHestonHullWhiteStrikesTests
{
    private static readonly Date Today = new(28, March, 2004);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();
    private static readonly YieldTermStructure RiskFree = new FlatForward(Today, 0.05, A365);

    private static readonly HestonModel Model = new(new HestonProcess(
        RiskFree, new FlatForward(Today, 0.02, A365), 100.0, 0.09, 1.0, 0.09, 0.3, -0.5));

    private static readonly HullWhiteProcess HullWhite = new(RiskFree, 0.00883, 0.01);

    private static readonly VanillaOption Reference =
        new(new PlainVanillaPayoff(OptionType.Call, 100.0), new EuropeanExercise(new Date(28, March, 2005)));

    private static readonly double[] Strikes = [100.0, 80.0, 90.0, 110.0, 120.0];

    [Fact]
    public void EachStrikeIsTheSolversValueAtTheScaledSpot()
    {
        ImmutableArray<OneAssetOptionResults> results = FdHestonHullWhiteVanillaEngine.CalculateStrikes(
            Reference, Strikes, Context, Model, HullWhite, -0.5, 20, 40, 10, 7, 0, false);

        FdmHestonHullWhiteSolver solver = MultiStrikeSolver(20, 40, 10, 7);
        HestonProcess process = Model.Process;
        double spot = process.S0, v0 = process.V0;

        Assert.Equal(Strikes.Length, results.Length);
        for (int i = 0; i < Strikes.Length; ++i)
        {
            double d = 100.0 / Strikes[i];
            Assert.Equal(solver.ValueAt(spot * d, v0, 0) / d, results[i].NPV);
            Assert.Equal(solver.DeltaAt(spot * d, v0, 0, spot * d * 0.01), results[i].Delta);
            Assert.Equal(solver.GammaAt(spot * d, v0, 0, spot * d * 0.01) * d, results[i].Gamma);
            Assert.Equal(solver.ThetaAt(spot * d, v0, 0) / d, results[i].Theta);
        }
    }

    [Fact]
    public void TheControlVariateCorrectsEachStrike()
    {
        ImmutableArray<OneAssetOptionResults> plain = FdHestonHullWhiteVanillaEngine.CalculateStrikes(
            Reference, Strikes, Context, Model, HullWhite, -0.5, 20, 40, 10, 7, 0, false);
        ImmutableArray<OneAssetOptionResults> corrected = FdHestonHullWhiteVanillaEngine.CalculateStrikes(
            Reference, Strikes, Context, Model, HullWhite, -0.5, 20, 40, 10, 7, 0, true);
        ImmutableArray<OneAssetOptionResults> fdHeston = FdHestonVanillaEngine.CalculateStrikes(
            Reference, Strikes, Context, Model, 20, 40, 10);

        for (int i = 0; i < Strikes.Length; ++i)
        {
            var option = new VanillaOption(new PlainVanillaPayoff(OptionType.Call, Strikes[i]), Reference.Exercise);
            double analytic = AnalyticHestonEngine.Calculate(option, Context, Model, 164).NPV!.Value;
            Assert.Equal(plain[i].NPV!.Value + (analytic - fdHeston[i].NPV!.Value), corrected[i].NPV);
            Assert.Equal(plain[i].Delta, corrected[i].Delta);
            Assert.Equal(plain[i].Gamma, corrected[i].Gamma);
        }
    }

    [Fact]
    public void TheResultsComeInTheStrikesOrder()
    {
        double[] permuted = [120.0, 90.0, 100.0, 80.0, 110.0];
        ImmutableArray<OneAssetOptionResults> results = FdHestonHullWhiteVanillaEngine.CalculateStrikes(
            Reference, Strikes, Context, Model, HullWhite, -0.5, 20, 40, 10, 7);
        ImmutableArray<OneAssetOptionResults> again = FdHestonHullWhiteVanillaEngine.CalculateStrikes(
            Reference, permuted, Context, Model, HullWhite, -0.5, 20, 40, 10, 7);

        for (int i = 0; i < permuted.Length; ++i)
        {
            int j = Array.IndexOf(Strikes, permuted[i]);
            Assert.Equal(results[j].NPV, again[i].NPV);
            Assert.Equal(results[j].Gamma, again[i].Gamma);
        }
    }

    [Fact]
    public void DiscreteDividendsAndNoStrikesAreRefused()
    {
        ImmutableArray<NanoQL.Coupons.Dividend> dividends =
            NanoQL.Coupons.DividendFunctions.DividendVector([Today + 90], [1.0]);
        ArgumentException refused = Assert.Throws<ArgumentException>(
            () => FdHestonHullWhiteVanillaEngine.CalculateStrikes(Reference, [100.0, 110.0], Context, Model, HullWhite, dividends, -0.5));
        Assert.Equal("dividends", refused.ParamName);
        Assert.StartsWith("multiple strikes engine does not work with discrete dividends", refused.Message, StringComparison.Ordinal);

        ArgumentException none = Assert.Throws<ArgumentException>(
            () => FdHestonHullWhiteVanillaEngine.CalculateStrikes(Reference, [], Context, Model, HullWhite, -0.5));
        Assert.Equal("strikes", none.ParamName);
        Assert.StartsWith("no strikes given", none.Message, StringComparison.Ordinal);
    }

    // QuantLib's engine with enableMultipleStrikesCaching(strikes), from the public parts, and its solver.
    private static FdmHestonHullWhiteSolver MultiStrikeSolver(int tGrid, int xGrid, int vGrid, int rGrid)
    {
        HestonProcess process = Model.Process;
        TermStructure clock = FdmHestonOp.Clock(process);
        double maturity = clock.TimeFromReference(Reference.Exercise.LastDate());

        var vMesher = new FdmHestonVarianceMesher(vGrid, process, maturity, Math.Max(5, tGrid / 50));
        // QuantLib's call, processHelper(s0, dividendYield, riskFreeRate, vol), its curves in each other's places
        var helper = FdmBlackScholesMesher.ProcessHelper(
            process.S0, process.DividendYield, process.RiskFreeRate, vMesher.VolaEstimate);
        var equityMesher = new FdmBlackScholesMultiStrikeMesher(xGrid, helper, maturity, Strikes, 0.0001, 1.5, (100.0, 0.075));
        var shortRateMesher = new FdmSimpleProcess1dMesher(rGrid, new OrnsteinUhlenbeckProcess(HullWhite.A, HullWhite.Sigma), maturity);
        var mesher = new FdmMesherComposite(equityMesher, vMesher, shortRateMesher);
        var calculator = new FdmLogInnerValue(Reference.Payoff!, mesher, 0);
        FdmStepConditionComposite conditions = FdmStepConditionComposite.VanillaComposite(
            [], Reference.Exercise, mesher, calculator, clock.ReferenceDate, clock.DayCounter, Context);
        var desc = new FdmSolverDesc(mesher, [], conditions, calculator, maturity, tGrid, 0);
        return new FdmHestonHullWhiteSolver(process, HullWhite, -0.5, desc);
    }
}
