// SPDX-FileCopyrightText: Copyright (C) 2009 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/fdhestonhullwhitevanillaengine.hpp ql/pricingengines/vanilla/fdhestonhullwhitevanillaengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Models;
using NanoQL.Processes;
using NanoQL.TermStructures;

namespace NanoQL.PricingEngines;

/// <summary>
/// The finite-difference Heston-Hull-White pricing engine for vanilla options, European, American or Bermudan, with
/// cash dividends optional: <see cref="FdmHestonHullWhiteSolver"/> on the log spot, the variance and the Hull-White
/// short rate's state, with a control variate, the analytic Heston engine's price less the finite-difference Heston
/// engine's, optional.
/// </summary>
/// <remarks>
/// <para>
/// The engine reports the value, delta, gamma and theta, the theta <see langword="null"/> when the problem's first
/// stopping time is time 0; the delta and gamma are central differences at a shift of 1% of the spot. Of the Hull-White
/// process it reads the reversion and the volatility only, its short rate's curve the Heston process's risk-free curve.
/// Its finite-difference working types are built inside each calculation and dropped after it.
/// </para>
/// <para>
/// Its times run on <see cref="FdmHestonOp.Clock"/>, from the earliest reference date of the Heston process's
/// structures, each accruing nothing before its own date, where QuantLib's run from the risk-free curve's; where none
/// is dated before the risk-free curve, they are QuantLib's
/// (<c>Divergences/HestonHullWhiteMismatchedTimesTests.cs</c>).
/// </para>
/// <para>
/// QuantLib sizes the equity mesher on a helper process whose curves its call binds in each other's places, as the
/// finite-difference Heston engine's does (<see cref="FdHestonVanillaEngine"/>), so where the carry is large against
/// the volatility its grid falls short of the distribution: a five-year call at 1.25 times the forward, volatility 0.1,
/// priced −6.32 at carry −0.30 where the analytic Heston-Hull-White engine gives 0.392 and 0 at carry 0.30 for 1.760,
/// and a year's put at 0.7 of the forward, carry −0.2, priced 0 for 0.0068. Here the mesher is QuantLib's wherever its
/// range holds the true forwards' band and the strike's, at half this engine's scale factor of 1.5, and widened to the
/// bands where it doesn't, and the multi-strike solve solves each strike alone where its grid doesn't hold the
/// forwards' band at the points it is read at (<c>Divergences/FdHestonHullWhiteHighCarryTests.cs</c>).
/// </para>
/// <para>
/// QuantLib's <c>enableMultipleStrikesCaching</c> serves later options at its strikes from one solve, each V(S·d)/d
/// with d = K/Kᵢ, the strike a later call is served depending on which option solved first, which a cache may not do
/// here: a value doesn't depend on what was computed before it. Here
/// <see cref="CalculateStrikes(VanillaOption, IEnumerable{double}, EvaluationContext, HestonModel, HullWhiteProcess, double, int, int, int, int, int, bool, FdmSchemeDesc?)"/>
/// returns every strike's results from one solve on the given option, each QuantLib's cached value bit for bit; after a
/// reference whose payoff isn't a plain vanilla, whose scaled values aren't the strikes' prices, each strike's plain
/// vanilla is solved alone, QuantLib's engine priced for that strike
/// (<c>Divergences/FdHestonHullWhiteStrikesReferenceTests.cs</c>).
/// </para>
/// </remarks>
public static class FdHestonHullWhiteVanillaEngine
{
    /// <summary>Prices a vanilla option.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The Heston model.</param>
    /// <param name="hwProcess">The Hull-White process, whose reversion and volatility are read.</param>
    /// <param name="corrEquityShortRate">The equity-rate correlation.</param>
    /// <param name="tGrid">The number of time steps.</param>
    /// <param name="xGrid">The number of the log spot's points.</param>
    /// <param name="vGrid">The number of the variance's points.</param>
    /// <param name="rGrid">The number of the short rate's points.</param>
    /// <param name="dampingSteps">The number of implicit damping steps.</param>
    /// <param name="controlVariate">Whether the control variate is used.</param>
    /// <param name="schemeDesc">The scheme; <see langword="null"/> for Hundsdorfer's, QuantLib's default.</param>
    /// <returns>The option's results; the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">The option, the context, the model or the Hull-White process is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A grid size or the damping steps is negative.</exception>
    /// <exception cref="ArgumentException">
    /// A grid is too small, the correlations' squares sum past 1, or the rollback's arguments are refused.
    /// </exception>
    /// <exception cref="NanoQLException">
    /// The payoff isn't striked, a step or the spline fails, or the control variate's analytic engine refuses the payoff.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option,
        EvaluationContext context,
        HestonModel model,
        HullWhiteProcess hwProcess,
        double corrEquityShortRate,
        int tGrid = 50,
        int xGrid = 100,
        int vGrid = 40,
        int rGrid = 20,
        int dampingSteps = 0,
        bool controlVariate = true,
        FdmSchemeDesc? schemeDesc = null) =>
        Calculate(
            option, context, model, hwProcess, [], corrEquityShortRate, tGrid, xGrid, vGrid, rGrid, dampingSteps,
            controlVariate, schemeDesc);

    /// <summary>Prices a vanilla option with cash dividends.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The Heston model.</param>
    /// <param name="hwProcess">The Hull-White process, whose reversion and volatility are read.</param>
    /// <param name="dividends">The cash dividends, by the spot model.</param>
    /// <param name="corrEquityShortRate">The equity-rate correlation.</param>
    /// <param name="tGrid">The number of time steps.</param>
    /// <param name="xGrid">The number of the log spot's points.</param>
    /// <param name="vGrid">The number of the variance's points.</param>
    /// <param name="rGrid">The number of the short rate's points.</param>
    /// <param name="dampingSteps">The number of implicit damping steps.</param>
    /// <param name="controlVariate">Whether the control variate is used.</param>
    /// <param name="schemeDesc">The scheme; <see langword="null"/> for Hundsdorfer's, QuantLib's default.</param>
    /// <returns>The option's results; the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">The option, the context, the model or the Hull-White process is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A grid size or the damping steps is negative.</exception>
    /// <exception cref="ArgumentException">
    /// The schedule is the default array or holds <see langword="null"/>, a grid is too small, the correlations' squares
    /// sum past 1, or the rollback's arguments are refused.
    /// </exception>
    /// <exception cref="NanoQLException">
    /// The payoff isn't striked, a step or the spline fails, or the control variate's analytic engine refuses the payoff.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option,
        EvaluationContext context,
        HestonModel model,
        HullWhiteProcess hwProcess,
        ImmutableArray<Dividend> dividends,
        double corrEquityShortRate,
        int tGrid = 50,
        int xGrid = 100,
        int vGrid = 40,
        int rGrid = 20,
        int dampingSteps = 0,
        bool controlVariate = true,
        FdmSchemeDesc? schemeDesc = null)
    {
        CheckArguments(option, context, model, hwProcess, dividends, tGrid, xGrid, vGrid, rGrid, dampingSteps);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(Solve(
            option, context, model, hwProcess, dividends, corrEquityShortRate, tGrid, xGrid, vGrid, rGrid, dampingSteps,
            controlVariate, schemeDesc));
    }

    /// <summary>
    /// The results of the option at each of several strikes from one solve on a mesher spanning them: QuantLib's
    /// multiple-strikes caching, as a call, each strike's value V(S·d)/d, delta, gamma·d and theta/d at d = K/Kᵢ, K the
    /// option's strike, the delta and gamma at a shift of 1% of S·d, and with the control variate each value corrected
    /// by the analytic Heston engine's price at Kᵢ less the finite-difference Heston engine's multiple-strikes value.
    /// </summary>
    /// <param name="option">The reference option, whose exercise the strikes share and whose option type their plain
    /// vanillas take.</param>
    /// <param name="strikes">The strikes, at least one.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The Heston model.</param>
    /// <param name="hwProcess">The Hull-White process, whose reversion and volatility are read.</param>
    /// <param name="corrEquityShortRate">The equity-rate correlation.</param>
    /// <param name="tGrid">The number of time steps.</param>
    /// <param name="xGrid">The number of the log spot's points.</param>
    /// <param name="vGrid">The number of the variance's points.</param>
    /// <param name="rGrid">The number of the short rate's points.</param>
    /// <param name="dampingSteps">The number of implicit damping steps.</param>
    /// <param name="controlVariate">Whether the control variate is used.</param>
    /// <param name="schemeDesc">The scheme; <see langword="null"/> for Hundsdorfer's, QuantLib's default.</param>
    /// <returns>The plain vanilla's results at each strike, in the strikes' order; the expired results for each when
    /// the option has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A grid size or the damping steps is negative.</exception>
    /// <exception cref="ArgumentException">
    /// No strike is given, a grid is too small, the correlations' squares sum past 1, or the rollback's arguments are
    /// refused.
    /// </exception>
    /// <exception cref="NanoQLException">The payoff isn't striked, or a step or the spline fails.</exception>
    public static ImmutableArray<OneAssetOptionResults> CalculateStrikes(
        VanillaOption option,
        IEnumerable<double> strikes,
        EvaluationContext context,
        HestonModel model,
        HullWhiteProcess hwProcess,
        double corrEquityShortRate,
        int tGrid = 50,
        int xGrid = 100,
        int vGrid = 40,
        int rGrid = 20,
        int dampingSteps = 0,
        bool controlVariate = true,
        FdmSchemeDesc? schemeDesc = null) =>
        CalculateStrikes(
            option, strikes, context, model, hwProcess, [], corrEquityShortRate, tGrid, xGrid, vGrid, rGrid, dampingSteps,
            controlVariate, schemeDesc);

    /// <summary>
    /// The results of the option at each of several strikes from one solve on a mesher spanning them, with QuantLib's
    /// refusal of cash dividends.
    /// </summary>
    /// <param name="option">The reference option, whose exercise the strikes share and whose option type their plain
    /// vanillas take.</param>
    /// <param name="strikes">The strikes, at least one.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The Heston model.</param>
    /// <param name="hwProcess">The Hull-White process, whose reversion and volatility are read.</param>
    /// <param name="dividends">The cash dividends: QuantLib's multiple-strikes engine refuses any.</param>
    /// <param name="corrEquityShortRate">The equity-rate correlation.</param>
    /// <param name="tGrid">The number of time steps.</param>
    /// <param name="xGrid">The number of the log spot's points.</param>
    /// <param name="vGrid">The number of the variance's points.</param>
    /// <param name="rGrid">The number of the short rate's points.</param>
    /// <param name="dampingSteps">The number of implicit damping steps.</param>
    /// <param name="controlVariate">Whether the control variate is used.</param>
    /// <param name="schemeDesc">The scheme; <see langword="null"/> for Hundsdorfer's, QuantLib's default.</param>
    /// <returns>The plain vanilla's results at each strike, in the strikes' order; the expired results for each when
    /// the option has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A grid size or the damping steps is negative.</exception>
    /// <exception cref="ArgumentException">
    /// No strike is given, dividends are given ("multiple strikes engine does not work with discrete dividends"), a grid
    /// is too small, the correlations' squares sum past 1, or the rollback's arguments are refused.
    /// </exception>
    /// <exception cref="NanoQLException">The payoff isn't striked, or a step or the spline fails.</exception>
    public static ImmutableArray<OneAssetOptionResults> CalculateStrikes(
        VanillaOption option,
        IEnumerable<double> strikes,
        EvaluationContext context,
        HestonModel model,
        HullWhiteProcess hwProcess,
        ImmutableArray<Dividend> dividends,
        double corrEquityShortRate,
        int tGrid = 50,
        int xGrid = 100,
        int vGrid = 40,
        int rGrid = 20,
        int dampingSteps = 0,
        bool controlVariate = true,
        FdmSchemeDesc? schemeDesc = null)
    {
        ArgumentNullException.ThrowIfNull(strikes);
        double[] k = [.. strikes];
        CheckArguments(option, context, model, hwProcess, dividends, tGrid, xGrid, vGrid, rGrid, dampingSteps);
        if (k.Length == 0)
        {
            throw new ArgumentException("no strikes given", nameof(strikes));
        }

        if (!dividends.IsEmpty)
        {
            throw new ArgumentException("multiple strikes engine does not work with discrete dividends", nameof(dividends));
        }

        if (option.IsExpired(context))
        {
            OneAssetOptionResults expired = option.SetupExpired();
            return [.. k.Select(_ => expired)];
        }

        option.Validate();
        if (option.Payoff is not StrikedTypePayoff payoff)
        {
            throw new NanoQLException("wrong payoff type given");
        }

        var builder = ImmutableArray.CreateBuilder<OneAssetOptionResults>(k.Length);
        HestonProcess process = model.Process;
        if (option.Payoff is not PlainVanillaPayoff
            || !FdHestonVanillaEngine.MultiStrikeGridHolds(option, model, k, null, tGrid, vGrid, 1.0, FdmHestonOp.Clock(process)))
        {
            // As the finite-difference Heston engine, after a reference that isn't a plain vanilla, and on a
            // multi-strike grid short of the true forwards' band: each strike's plain vanilla solved alone, QuantLib's
            // engine priced for that strike
            foreach (double strike in k)
            {
                var single = new VanillaOption(new PlainVanillaPayoff(payoff.OptionType, strike), option.Exercise);
                builder.Add(single.FetchResults(Solve(
                    single, context, model, hwProcess, dividends, corrEquityShortRate, tGrid, xGrid, vGrid, rGrid,
                    dampingSteps, controlVariate, schemeDesc)));
            }

            return builder.MoveToImmutable();
        }

        FdmHestonHullWhiteSolver solver = BuildSolver(
            option, context, model, hwProcess, dividends, corrEquityShortRate, tGrid, xGrid, vGrid, rGrid, dampingSteps,
            schemeDesc, k);

        double spot = process.S0;
        double v0 = process.V0;
        ImmutableArray<OneAssetOptionResults> fdHeston = [];
        var exercise = new EuropeanExercise(option.Exercise.LastDate());
        if (controlVariate)
        {
            fdHeston = FdHestonVanillaEngine.CalculateStrikes(
                new VanillaOption(payoff, exercise), k, context, model, tGrid, xGrid, vGrid, dampingSteps, schemeDesc);
        }

        for (int i = 0; i < k.Length; ++i)
        {
            double d = payoff.Strike / k[i];

            double value = solver.ValueAt(spot * d, v0, 0) / d;
            if (controlVariate)
            {
                var controlVariateOption = new VanillaOption(new PlainVanillaPayoff(payoff.OptionType, k[i]), exercise);
                double analyticNPV = AnalyticHestonEngine.Calculate(controlVariateOption, context, model, 164).NPV!.Value;
                double fdNPV = fdHeston[i].NPV!.Value;
                value += analyticNPV - fdNPV;
            }

            builder.Add(new OneAssetOptionResults
            {
                NPV = value,
                Delta = solver.DeltaAt(spot * d, v0, 0, spot * d * 0.01),
                Gamma = solver.GammaAt(spot * d, v0, 0, spot * d * 0.01) * d,
                Theta = solver.ThetaAt(spot * d, v0, 0) / d,
            });
        }

        return builder.MoveToImmutable();
    }

    // the argument checks every member makes first
    private static void CheckArguments(
        VanillaOption option,
        EvaluationContext context,
        HestonModel model,
        HullWhiteProcess hwProcess,
        ImmutableArray<Dividend> dividends,
        int tGrid,
        int xGrid,
        int vGrid,
        int rGrid,
        int dampingSteps)
    {
        ArgumentNullException.ThrowIfNull(hwProcess);
        ArgumentOutOfRangeException.ThrowIfNegative(rGrid);
        FdHestonVanillaEngine.CheckArguments(option, context, model, dividends, tGrid, xGrid, vGrid, dampingSteps);
    }

    // QuantLib's calculate after the cache lookup: the solve, read at the spot, v0 and the short rate's state 0, and
    // the control variate
    private static OneAssetOptionResults Solve(
        VanillaOption option,
        EvaluationContext context,
        HestonModel model,
        HullWhiteProcess hwProcess,
        ImmutableArray<Dividend> dividends,
        double corrEquityShortRate,
        int tGrid,
        int xGrid,
        int vGrid,
        int rGrid,
        int dampingSteps,
        bool controlVariate,
        FdmSchemeDesc? schemeDesc)
    {
        HestonProcess process = model.Process;
        FdmHestonHullWhiteSolver solver = BuildSolver(
            option, context, model, hwProcess, dividends, corrEquityShortRate, tGrid, xGrid, vGrid, rGrid, dampingSteps,
            schemeDesc, null);

        double spot = process.S0;
        double v0 = process.V0;
        double value = solver.ValueAt(spot, v0, 0);
        var results = new OneAssetOptionResults
        {
            Delta = solver.DeltaAt(spot, v0, 0, spot * 0.01),
            Gamma = solver.GammaAt(spot, v0, 0, spot * 0.01),
            Theta = solver.ThetaAt(spot, v0, 0),
        };

        if (controlVariate)
        {
            // the payoff striked, BuildSolver's check
            var controlVariateOption = new VanillaOption(
                (StrikedTypePayoff)option.Payoff!, new EuropeanExercise(option.Exercise.LastDate()));
            double analyticNPV = AnalyticHestonEngine.Calculate(controlVariateOption, context, model, 164).NPV!.Value;
            double fdNPV = FdHestonVanillaEngine.Calculate(
                controlVariateOption, context, model, tGrid, xGrid, vGrid, dampingSteps, schemeDesc).NPV!.Value;
            value += analyticNPV - fdNPV;
        }

        return results with { NPV = value };
    }

    private static FdmHestonHullWhiteSolver BuildSolver(
        VanillaOption option,
        EvaluationContext context,
        HestonModel model,
        HullWhiteProcess hwProcess,
        ImmutableArray<Dividend> dividends,
        double corrEquityShortRate,
        int tGrid,
        int xGrid,
        int vGrid,
        int rGrid,
        int dampingSteps,
        FdmSchemeDesc? schemeDesc,
        IReadOnlyList<double>? strikes)
    {
        // 2. Mesher
        HestonProcess process = model.Process;
        TermStructure clock = FdmHestonOp.Clock(process);
        double maturity = clock.TimeFromReference(option.Exercise.LastDate());

        // 2.1 The variance mesher
        const int TGridMin = 5;
        var varianceMesher = new FdmHestonVarianceMesher(vGrid, process, maturity, Math.Max(TGridMin, tGrid / 50));

        // 2.2 The equity mesher
        if (option.Payoff is not StrikedTypePayoff payoff)
        {
            throw new NanoQLException("wrong payoff type given");
        }

        (GeneralizedBlackScholesProcess helperProcess, GeneralizedBlackScholesProcess trueProcess) =
            FdHestonVanillaEngine.HelperProcesses(process, varianceMesher.VolaEstimate, clock);

        Fdm1dMesher equityMesher;
        if (strikes is null)
        {
            equityMesher = FdHestonVanillaEngine.EquityMesher(
                xGrid, helperProcess, trueProcess, maturity, payoff.Strike, 1.5, dividends, null, context, clock);
        }
        else
        {
            equityMesher = new FdmBlackScholesMultiStrikeMesher(
                xGrid, helperProcess, maturity, strikes, 0.0001, 1.5, (payoff.Strike, 0.075), clock);
        }

        // 2.3 The short rate mesher
        var ouProcess = new OrnsteinUhlenbeckProcess(hwProcess.A, hwProcess.Sigma);
        var shortRateMesher = new FdmSimpleProcess1dMesher(rGrid, ouProcess, maturity);

        var mesher = new FdmMesherComposite(equityMesher, varianceMesher, shortRateMesher);

        // 3. Calculator
        var calculator = new FdmLogInnerValue(option.Payoff, mesher, 0);

        // 4. Step conditions
        FdmStepConditionComposite conditions = FdmStepConditionComposite.VanillaComposite(
            dividends, option.Exercise, mesher, calculator, clock.ReferenceDate, clock.DayCounter, context);

        // 5. Boundary conditions
        ImmutableArray<BoundaryCondition<FdmLinearOp>> boundaries = [];

        // 6. Solver
        var solverDesc = new FdmSolverDesc(mesher, boundaries, conditions, calculator, maturity, tGrid, dampingSteps);
        return new FdmHestonHullWhiteSolver(process, hwProcess, corrEquityShortRate, solverDesc, schemeDesc);
    }
}
