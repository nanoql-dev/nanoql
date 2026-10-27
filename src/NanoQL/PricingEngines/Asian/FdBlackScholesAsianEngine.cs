// SPDX-FileCopyrightText: Copyright (C) 2009 Ralph Schreyer
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/asian/fdblackscholesasianengine.hpp ql/pricingengines/asian/fdblackscholesasianengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The finite-difference Black-Scholes engine for discrete arithmetic average-price Asian options, on a mesher of the
/// log spot and the log average with the average's step condition at each fixing.
/// </summary>
/// <remarks>
/// <para>
/// The value, delta and gamma, as QuantLib's. Its times run on the engine's clock,
/// <see cref="FdmBlackScholesOp.Clock"/>, from the earliest reference date of the structures, where QuantLib takes them
/// from the process (the S-bar plan's BR5 (xxvi), <c>Divergences/AsianMismatchedTimesTests.cs</c>); several fixings on
/// one date are weighed by their count (BR5 (xxviii), <see cref="FdmArithmeticAverageCondition"/>).
/// </para>
/// <para>
/// A fixing dated from the evaluation date to the clock's origin, where the structures settle after the evaluation
/// date, is taken at the origin, the spot with no variance and no discount accrued, so as a past fixing at the spot
/// (the S-bar code review's B3-1, <c>Divergences/AsianLateSettlementTests.cs</c>), where QuantLib refuses it as past.
/// </para>
/// </remarks>
public static class FdBlackScholesAsianEngine
{
    /// <summary>Prices a discrete arithmetic average-price Asian option.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="tGrid">The number of time steps.</param>
    /// <param name="xGrid">The number of spot grid points.</param>
    /// <param name="aGrid">The number of average grid points.</param>
    /// <param name="schemeDesc">The scheme; Douglas's by default.</param>
    /// <returns>The option's results: the value, delta and gamma; the expired results when the option has
    /// expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A grid size is negative.</exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't European, the average isn't arithmetic, a running average has no past fixing, the spot isn't
    /// positive, or the option's past fixings fail its checks.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        DiscreteAveragingAsianOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        int tGrid = 100,
        int xGrid = 100,
        int aGrid = 50,
        FdmSchemeDesc? schemeDesc = null)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        ArgumentOutOfRangeException.ThrowIfNegative(tGrid);
        ArgumentOutOfRangeException.ThrowIfNegative(xGrid);
        ArgumentOutOfRangeException.ThrowIfNegative(aGrid);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        Date origin = FdmBlackScholesOp.Clock(process).ReferenceDate;
        DiscreteAveragingAsianArguments arguments = option.Arguments(context).AtOrigin(
            context.EvaluationDate, d => d < origin, process.X0);
        return option.FetchResults(Body(option, arguments, context, process, tGrid, xGrid, aGrid, schemeDesc ?? FdmSchemeDesc.Douglas()));
    }

    private static OneAssetOptionResults Body(
        DiscreteAveragingAsianOption option,
        DiscreteAveragingAsianArguments arguments,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        int tGrid,
        int xGrid,
        int aGrid,
        FdmSchemeDesc schemeDesc)
    {
        if (!(option.Exercise.Type == ExerciseType.European))
        {
            throw new NanoQLException("European exercise supported only");
        }

        if (!(arguments.AverageType == AverageType.Arithmetic))
        {
            throw new NanoQLException("Arithmetic averaging supported only");
        }

        if (!(arguments.RunningAccumulator == 0 || arguments.PastFixings > 0))
        {
            throw new NanoQLException("Running average requires at least one past fixing");
        }

        // 1. Mesher
        var payoff = (StrikedTypePayoff)option.Payoff!;
        TermStructure clock = FdmBlackScholesOp.Clock(process);
        Date lastDate = option.Exercise.LastDate();
        double maturity = clock.TimeFromReference(lastDate);
        var equityMesher = new FdmBlackScholesMesher(xGrid, process, maturity, payoff.Strike, context: context);

        double spot = process.X0;
        if (!(spot > 0.0))
        {
            throw new NanoQLException("negative or null underlying given");
        }

        double avg = arguments.RunningAccumulator == 0 ? spot : arguments.RunningAccumulator / arguments.PastFixings;

        double normInvEps = new InverseCumulativeNormal().Evaluate(1 - 0.0001);

        // the standard deviation to the maturity date, the volatility's own time to it (QuantLib's product where the
        // clocks agree)
        double sigmaSqrtT = process.BlackVolatility.BlackVol(lastDate, payoff.Strike) * Math.Sqrt(process.BlackVolatility.TimeFromReference(lastDate));
        double r = sigmaSqrtT * normInvEps;

        double xMin = Math.Min(Math.Log(avg) - (0.25 * r), Math.Log(spot) - (1.5 * r));
        double xMax = Math.Max(Math.Log(avg) + (0.25 * r), Math.Log(spot) + (1.5 * r));

        var averageMesher = new FdmBlackScholesMesher(aGrid, process, maturity, payoff.Strike, xMin, xMax, context: context);

        var mesher = new FdmMesherComposite(equityMesher, averageMesher);

        // 2. Calculator
        var calculator = new FdmLogInnerValue(payoff, mesher, 1);

        // 3. Step conditions
        // 3.1 Arithmetic average step conditions
        ImmutableArray<double>.Builder averageTimes = ImmutableArray.CreateBuilder<double>(arguments.FixingDates.Length);
        foreach (Date fixingDate in arguments.FixingDates)
        {
            double t = clock.TimeFromReference(fixingDate);
            if (!(t >= 0))
            {
                throw new NanoQLException("Fixing dates must not contain past date");
            }

            averageTimes.Add(t);
        }

        ImmutableArray<double> times = averageTimes.ToImmutable();
        var conditions = new FdmStepConditionComposite(
            [times],
            [new FdmArithmeticAverageCondition(times, arguments.RunningAccumulator, arguments.PastFixings, mesher, 0)]);

        // 4. Boundary conditions
        ImmutableArray<BoundaryCondition<FdmLinearOp>> boundaries = [];

        // 5. Solver
        var solverDesc = new FdmSolverDesc(mesher, boundaries, conditions, calculator, maturity, tGrid, 0);
        var solver = new FdmSimple2dBSSolver(process, payoff.Strike, solverDesc, schemeDesc);

        return new OneAssetOptionResults
        {
            NPV = solver.ValueAt(spot, avg),
            Delta = solver.DeltaAt(spot, avg, spot * 0.01),
            Gamma = solver.GammaAt(spot, avg, spot * 0.01),
        };
    }
}
