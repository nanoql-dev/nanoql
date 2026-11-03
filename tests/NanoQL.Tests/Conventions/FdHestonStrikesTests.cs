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
/// QuantLib's multiple-strikes caching as a call:
/// <see cref="FdHestonVanillaEngine.CalculateStrikes(VanillaOption, IEnumerable{double}, EvaluationContext, HestonModel, int, int, int, int, FdmSchemeDesc?, LocalVolTermStructure?, double)"/>
/// solves once on QuantLib's multi-strike mesher and returns each strike's results in the strikes' order: the value
/// V(S·d)/d, delta, gamma·d and theta/d at d = K/Kᵢ, K the reference option's strike, bit for bit the solver's on the
/// problem QuantLib's <c>getSolverDesc</c> builds with the strikes, the reference strike's own result at d = 1; and
/// QuantLib's refusals, of discrete dividends and of no strikes.
/// </summary>
public class FdHestonStrikesTests
{
    private static readonly Date Today = new(27, December, 2004);
    private static readonly EvaluationContext Context = new() { EvaluationDate = Today };
    private static readonly DayCounter A365 = new Actual365Fixed();

    private static readonly HestonModel Model = new(new HestonProcess(
        new FlatForward(Today, 0.06, A365), new FlatForward(Today, 0.02, A365), 1.05, 0.16, 2.5, 0.09, 0.8, -0.8));

    private static readonly VanillaOption Reference =
        new(new PlainVanillaPayoff(OptionType.Put, 1.0), new EuropeanExercise(new Date(28, March, 2006)));

    [Fact]
    public void EachStrikeIsTheSolversValueAtTheScaledSpot()
    {
        double[] strikes = [1.0, 0.5, 0.75, 1.5, 2.0];
        ImmutableArray<OneAssetOptionResults> results =
            FdHestonVanillaEngine.CalculateStrikes(Reference, strikes, Context, Model, 20, 60, 20);

        FdmHestonSolver solver = MultiStrikeSolver(strikes, 20, 60, 20);
        HestonProcess process = Model.Process;
        double spot = process.S0, v0 = process.V0;

        Assert.Equal(strikes.Length, results.Length);
        for (int i = 0; i < strikes.Length; ++i)
        {
            double d = 1.0 / strikes[i];
            Assert.Equal(solver.ValueAt(spot * d, v0) / d, results[i].NPV);
            Assert.Equal(solver.DeltaAt(spot * d, v0), results[i].Delta);
            Assert.Equal(solver.GammaAt(spot * d, v0) * d, results[i].Gamma);
            Assert.Equal(solver.ThetaAt(spot * d, v0) / d, results[i].Theta);
        }

        // the reference strike's own result: the solver at the spot itself
        Assert.Equal(solver.ValueAt(spot, v0), results[0].NPV);
        Assert.Equal(solver.DeltaAt(spot, v0), results[0].Delta);
    }

    [Fact]
    public void TheResultsComeInTheStrikesOrder()
    {
        double[] strikes = [1.0, 0.5, 0.75, 1.5, 2.0];
        double[] permuted = [2.0, 0.75, 1.0, 0.5, 1.5];
        ImmutableArray<OneAssetOptionResults> results =
            FdHestonVanillaEngine.CalculateStrikes(Reference, strikes, Context, Model, 20, 60, 20);
        ImmutableArray<OneAssetOptionResults> again =
            FdHestonVanillaEngine.CalculateStrikes(Reference, permuted, Context, Model, 20, 60, 20);

        for (int i = 0; i < permuted.Length; ++i)
        {
            int j = Array.IndexOf(strikes, permuted[i]);
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
            () => FdHestonVanillaEngine.CalculateStrikes(Reference, [1.0, 1.5], Context, Model, dividends, null, 20, 60, 20));
        Assert.Equal("dividends", refused.ParamName);
        Assert.StartsWith("multiple strikes engine does not work with discrete dividends", refused.Message, StringComparison.Ordinal);

        ArgumentException none = Assert.Throws<ArgumentException>(
            () => FdHestonVanillaEngine.CalculateStrikes(Reference, [], Context, Model, 20, 60, 20));
        Assert.Equal("strikes", none.ParamName);
        Assert.StartsWith("no strikes given", none.Message, StringComparison.Ordinal);
    }

    // QuantLib's getSolverDesc with enableMultipleStrikesCaching(strikes), from the public parts, and its solver.
    private static FdmHestonSolver MultiStrikeSolver(double[] strikes, int tGrid, int xGrid, int vGrid)
    {
        HestonProcess process = Model.Process;
        TermStructure clock = FdmHestonOp.Clock(process);
        double maturity = clock.TimeFromReference(Reference.Exercise.LastDate());

        var vMesher = new FdmHestonLocalVolatilityVarianceMesher(vGrid, process, null, maturity, Math.Max(5, tGrid / 50));
        // QuantLib's call, processHelper(s0, dividendYield, riskFreeRate, vol), its curves in each other's places
        var helper = FdmBlackScholesMesher.ProcessHelper(
            process.S0, process.DividendYield, process.RiskFreeRate, vMesher.VolaEstimate);
        var equityMesher = new FdmBlackScholesMultiStrikeMesher(xGrid, helper, maturity, strikes, 0.0001, 1.5, (1.0, 0.075));
        var mesher = new FdmMesherComposite(equityMesher, vMesher);
        var calculator = new FdmLogInnerValue(Reference.Payoff!, mesher, 0);
        FdmStepConditionComposite conditions = FdmStepConditionComposite.VanillaComposite(
            [], Reference.Exercise, mesher, calculator, clock.ReferenceDate, clock.DayCounter, Context);
        var desc = new FdmSolverDesc(mesher, [], conditions, calculator, maturity, tGrid, 0);
        return new FdmHestonSolver(process, desc);
    }
}
