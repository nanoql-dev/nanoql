// SPDX-FileCopyrightText: Copyright (C) 2021 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/fdblackscholesshoutengine.hpp ql/pricingengines/vanilla/fdblackscholesshoutengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The finite-difference pricing engine for shout options under the Black-Scholes process, with cash dividends by the
/// escrowed model: the holder may once lock in the intrinsic value, paid at maturity, and keep the option on the rest of
/// the move (<see cref="FdmShoutLogInnerValueCalculator"/>), the shout window the option's exercise.
/// </summary>
/// <remarks>
/// <para>
/// The engine reports the value, delta, gamma and theta; the theta is <see langword="null"/> when the problem's first
/// stopping time is time 0, as QuantLib's is. Its finite-difference working types are built inside each calculation and
/// dropped after it (the S-eq plan's SEQR1).
/// </para>
/// <para>
/// Its times run on <see cref="FdmBlackScholesOp.Clock"/>, from the earliest reference date of the structures it reads,
/// each accruing nothing before its own date; where no structure is dated before the risk-free curve, they are
/// QuantLib's. The residual option after a shout is struck at the spot, where QuantLib strikes it at the escrowed level
/// (<see cref="FdmShoutLogInnerValueCalculator"/>'s remarks; the S-van plan's VR4 (xxx)).
/// </para>
/// <para>
/// QuantLib's two constructors are one method here: the dividends are an optional argument.
/// </para>
/// </remarks>
public static class FdBlackScholesShoutEngine
{
    /// <summary>Prices a shout option.</summary>
    /// <param name="option">The option: a plain vanilla payoff, the exercise its shout window.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="dividends">The cash dividends, by the escrowed model; the default array for none.</param>
    /// <param name="tGrid">The number of time steps.</param>
    /// <param name="xGrid">The number of the underlying's points.</param>
    /// <param name="dampingSteps">The number of implicit damping steps.</param>
    /// <param name="schemeDesc">The scheme; <see langword="null"/> for Douglas, QuantLib's default.</param>
    /// <returns>The option's results; the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">The option, the context or the process is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A grid size or the damping steps is negative.</exception>
    /// <exception cref="ArgumentException">The schedule holds <see langword="null"/>, or a grid is too small.</exception>
    /// <exception cref="NanoQLException">
    /// The spot less the dividends isn't positive, the payoff isn't plain vanilla, or a step or the spline fails.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        ImmutableArray<Dividend> dividends = default,
        int tGrid = 100,
        int xGrid = 100,
        int dampingSteps = 0,
        FdmSchemeDesc? schemeDesc = null)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        ImmutableArray<Dividend> schedule = dividends.IsDefault ? [] : dividends;
        if (schedule.Any(d => d is null))
        {
            throw new ArgumentException("null dividend in schedule", nameof(dividends));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(tGrid);
        ArgumentOutOfRangeException.ThrowIfNegative(xGrid);
        ArgumentOutOfRangeException.ThrowIfNegative(dampingSteps);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        OneAssetOptionResults results = Body(
            option, context, process, schedule, tGrid, xGrid, dampingSteps, schemeDesc ?? FdmSchemeDesc.Douglas());
        return option.FetchResults(results);
    }

    private static OneAssetOptionResults Body(
        VanillaOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        ImmutableArray<Dividend> dividends,
        int tGrid,
        int xGrid,
        int dampingSteps,
        FdmSchemeDesc schemeDesc)
    {
        // the times the engine's clock's (FdmBlackScholesOp.Clock), the process's where no structure is dated before the
        // risk-free curve
        TermStructure clock = FdmBlackScholesOp.Clock(process);
        Date exerciseDate = option.Exercise.LastDate();
        double maturity = clock.TimeFromReference(exerciseDate);
        Date settlementDate = clock.ReferenceDate;

        var escrowedDividendAdj = new EscrowedDividendAdjustment(
            dividends, process.RiskFreeRate, process.DividendYield, clock.TimeFromReference, maturity, context, clock);

        double divAdj = escrowedDividendAdj.DividendAdjustment(clock.TimeFromReference(settlementDate));

        if (!(process.X0 + divAdj > 0.0))
        {
            throw new NanoQLException("spot minus dividends becomes negative");
        }

        if (option.Payoff is not PlainVanillaPayoff payoff)
        {
            throw new NanoQLException("non plain vanilla payoff given");
        }

        var mesher = new FdmMesherComposite(
            new FdmBlackScholesMesher(
                xGrid,
                process,
                maturity,
                payoff.Strike,
                null,
                null,
                0.0001,
                1.5,
                (payoff.Strike, 0.1),
                [],
                null,
                divAdj,
                context));

        var innerValuecalculator = new FdmShoutLogInnerValueCalculator(
            process.BlackVolatility, escrowedDividendAdj, maturity, payoff, mesher, 0);

        ImmutableArray<Dividend> zeroDividendSchedule =
            [.. dividends.Select(cf => (Dividend)new FixedDividend(0.0, cf.Date))];

        FdmStepConditionComposite conditions = FdmStepConditionComposite.VanillaComposite(
            zeroDividendSchedule,
            option.Exercise,
            mesher,
            innerValuecalculator,
            clock.ReferenceDate,
            clock.DayCounter,
            context);

        var solverDesc = new FdmSolverDesc(
            mesher, [], conditions, innerValuecalculator, maturity, tGrid, dampingSteps);

        var solver = new FdmBlackScholesSolver(process, payoff.Strike, solverDesc, schemeDesc);

        double spot = process.X0 + divAdj;

        return new OneAssetOptionResults
        {
            NPV = solver.ValueAt(spot),
            Delta = solver.DeltaAt(spot),
            Gamma = solver.GammaAt(spot),
            Theta = solver.ThetaAt(spot),
        };
    }
}
