// SPDX-FileCopyrightText: Copyright (C) 2008 Andreas Gaida
// SPDX-FileCopyrightText: Copyright (C) 2008 Ralph Schreyer
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Klaus Spanderen
// SPDX-FileCopyrightText: Copyright (C) 2008, 2009 Ralph Schreyer
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/fdblackscholesvanillaengine.hpp ql/pricingengines/vanilla/fdblackscholesvanillaengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Processes;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The finite-difference pricing engine for vanilla options under the Black-Scholes process, European, American or
/// Bermudan, with cash dividends by the spot or the escrowed model, on the Black or local volatility, and optionally
/// quanto.
/// </summary>
/// <remarks>
/// <para>
/// The engine reports the value, delta, gamma and theta; the theta is <see langword="null"/> when the problem's first
/// stopping time is time 0, as QuantLib's is; the other Greeks are <see langword="null"/>. Its finite-difference
/// working types are built inside each calculation and dropped after it (the S-eq plan's SEQR1).
/// </para>
/// <para>
/// Its times run on <see cref="FdmBlackScholesOp.Clock"/>, from the earliest reference date of the structures it reads,
/// each structure accruing nothing before its own date; where no structure is dated before the risk-free curve, they
/// are QuantLib's (<c>Divergences/FdmMismatchedTimesTests.cs</c>). The theta is the change over the clock's first
/// calendar day, QuantLib's own engine's theta on the market restated from that date: where a structure is dated after
/// the evaluation date, that day accrues none of its dividends or variance, so the theta differs from the formula theta
/// of the analytic and binomial engines, even in sign (+2.47 against the analytic engine's −27.57 on a 14-day put whose
/// dividend curve and volatility start four days later).
/// </para>
/// <para>
/// QuantLib's four constructors are one method here: the dividends and the quanto helper are optional arguments.
/// </para>
/// </remarks>
public static class FdBlackScholesVanillaEngine
{
    /// <summary>Prices a vanilla option by finite differences.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context, which the dividends' amounts take.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="dividends">The cash dividends; <c>default</c> or empty for none.</param>
    /// <param name="quantoHelper">The quanto helper, or <see langword="null"/> for none.</param>
    /// <param name="tGrid">The number of time steps.</param>
    /// <param name="xGrid">The number of points of the grid in the underlying's logarithm.</param>
    /// <param name="dampingSteps">The number of implicit Euler damping steps.</param>
    /// <param name="schemeDesc">The scheme; Douglas's by default.</param>
    /// <param name="localVol">Whether to use the process's local volatility.</param>
    /// <param name="illegalLocalVolOverwrite">
    /// The volatility to use where the local volatility can't be computed; <see langword="null"/> (QuantLib's
    /// <c>-Null&lt;Real&gt;()</c>) or a negative value to let the failure through.
    /// </param>
    /// <param name="cashDividendModel">The cash dividends' model.</param>
    /// <returns>The option's results.</returns>
    /// <exception cref="ArgumentNullException">The option, the context or the process is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">
    /// A grid size or the damping steps are negative, or the model or the scheme isn't a member of its enum.
    /// </exception>
    /// <exception cref="ArgumentException">
    /// A dividend is <see langword="null"/>; the escrowed model is asked for a quanto option; the grid has fewer than
    /// two points; or the rollback's arguments are refused.
    /// </exception>
    /// <exception cref="NanoQLException">
    /// The payoff isn't striked, the spot less the escrowed dividends isn't positive, or a curve, the volatility, a
    /// step or the spline fails.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        ImmutableArray<Dividend> dividends = default,
        FdmQuantoHelper? quantoHelper = null,
        int tGrid = 100,
        int xGrid = 100,
        int dampingSteps = 0,
        FdmSchemeDesc? schemeDesc = null,
        bool localVol = false,
        double? illegalLocalVolOverwrite = null,
        FdBlackScholesVanillaEngineCashDividendModel cashDividendModel = FdBlackScholesVanillaEngineCashDividendModel.Spot)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process);
        ImmutableArray<Dividend> schedule = Dividends(dividends, nameof(dividends));
        ArgumentOutOfRangeException.ThrowIfNegative(tGrid);
        ArgumentOutOfRangeException.ThrowIfNegative(xGrid);
        ArgumentOutOfRangeException.ThrowIfNegative(dampingSteps);
        EnumArguments.Check(cashDividendModel, nameof(cashDividendModel));
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        OneAssetOptionResults results = Body(
            option,
            context,
            process,
            schedule,
            quantoHelper,
            tGrid,
            xGrid,
            dampingSteps,
            schemeDesc ?? FdmSchemeDesc.Douglas(),
            localVol,
            illegalLocalVolOverwrite,
            cashDividendModel);
        return option.FetchResults(results);
    }

    /// <summary>QuantLib's <c>calculate</c>: the engine's body, without the instrument's expiry check.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context, which the dividends' amounts take.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="dividends">The cash dividends.</param>
    /// <param name="quantoHelper">The quanto helper, or <see langword="null"/>.</param>
    /// <param name="tGrid">The number of time steps.</param>
    /// <param name="xGrid">The number of points of the grid.</param>
    /// <param name="dampingSteps">The number of damping steps.</param>
    /// <param name="schemeDesc">The scheme.</param>
    /// <param name="localVol">Whether to use the local volatility.</param>
    /// <param name="illegalLocalVolOverwrite">The volatility where the local volatility can't be computed.</param>
    /// <param name="cashDividendModel">The cash dividends' model.</param>
    /// <returns>The option's results.</returns>
    internal static OneAssetOptionResults Body(
        VanillaOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process,
        ImmutableArray<Dividend> dividends,
        FdmQuantoHelper? quantoHelper,
        int tGrid,
        int xGrid,
        int dampingSteps,
        FdmSchemeDesc schemeDesc,
        bool localVol,
        double? illegalLocalVolOverwrite,
        FdBlackScholesVanillaEngineCashDividendModel cashDividendModel)
    {
        // 0. Cash dividend model; the times the engine's clock's (FdmBlackScholesOp.Clock), from the earliest reference
        // date of the structures it reads, the process's where none is dated before the risk-free curve
        TermStructure clock = FdmBlackScholesOp.Clock(process, quantoHelper);
        Date exerciseDate = option.Exercise.LastDate();
        double maturity = clock.TimeFromReference(exerciseDate);
        Date settlementDate = clock.ReferenceDate;

        double spotAdjustment = 0.0;
        ImmutableArray<Dividend> dividendSchedule = [];

        EscrowedDividendAdjustment? escrowedDivAdj = null;

        switch (cashDividendModel)
        {
            case FdBlackScholesVanillaEngineCashDividendModel.Spot:
                dividendSchedule = dividends;
                break;
            case FdBlackScholesVanillaEngineCashDividendModel.Escrowed:
                if (option.Exercise.Type != ExerciseType.European)
                {
                    // add dividend dates as stopping times
                    dividendSchedule = [.. dividends.Select(cf => (Dividend)new FixedDividend(0.0, cf.Date))];
                }

                if (!(quantoHelper is null))
                {
                    throw new ArgumentException(
                        "Escrowed dividend model is not supported for Quanto-Options", nameof(quantoHelper));
                }

                escrowedDivAdj = new EscrowedDividendAdjustment(
                    dividends,
                    process.RiskFreeRate,
                    process.DividendYield,
                    clock.TimeFromReference,
                    maturity,
                    context,
                    clock);

                spotAdjustment = escrowedDivAdj.DividendAdjustment(clock.TimeFromReference(settlementDate));

                if (!(process.X0 + spotAdjustment > 0.0))
                {
                    throw new NanoQLException("spot minus dividends becomes negative");
                }

                break;
            default:
                throw new NanoQLException("unknwon cash dividend model");
        }

        // 1. Mesher
        if (option.Payoff is not StrikedTypePayoff payoff)
        {
            throw new NanoQLException("non-striked payoff given");
        }

        var equityMesher = new FdmBlackScholesMesher(
            xGrid,
            process,
            maturity,
            payoff.Strike,
            null,
            null,
            0.0001,
            1.5,
            (payoff.Strike, 0.1),
            dividendSchedule,
            quantoHelper,
            spotAdjustment,
            context);

        var mesher = new FdmMesherComposite(equityMesher);

        // 2. Calculator
        FdmInnerValueCalculator calculator = new FdmLogInnerValue(payoff, mesher, 0);

        FdmInnerValueCalculator earlyExerciseCalculator = cashDividendModel switch
        {
            FdBlackScholesVanillaEngineCashDividendModel.Spot => calculator,
            _ => new FdmEscrowedLogInnerValueCalculator(escrowedDivAdj!, payoff, mesher, 0),
        };

        // 3. Step conditions
        FdmStepConditionComposite conditions = FdmStepConditionComposite.VanillaComposite(
            dividendSchedule,
            option.Exercise,
            mesher,
            earlyExerciseCalculator,
            clock.ReferenceDate,
            clock.DayCounter,
            context);

        // 4. Boundary conditions
        ImmutableArray<BoundaryCondition<FdmLinearOp>> boundaries = [];

        // 5. Solver
        var solverDesc = new FdmSolverDesc(mesher, boundaries, conditions, calculator, maturity, tGrid, dampingSteps);

        var solver = new FdmBlackScholesSolver(
            process, payoff.Strike, solverDesc, schemeDesc, localVol, illegalLocalVolOverwrite, quantoHelper);

        double spot = process.X0 + spotAdjustment;

        return new OneAssetOptionResults
        {
            NPV = solver.ValueAt(spot),
            Delta = solver.DeltaAt(spot),
            Gamma = solver.GammaAt(spot),
            Theta = solver.ThetaAt(spot),
        };
    }

    // an optional schedule: default for none
    private static ImmutableArray<Dividend> Dividends(ImmutableArray<Dividend> dividends, string paramName)
    {
        ImmutableArray<Dividend> schedule = dividends.IsDefault ? [] : dividends;
        if (schedule.Any(d => d is null))
        {
            throw new ArgumentException("null dividend in schedule", paramName);
        }

        return schedule;
    }
}
