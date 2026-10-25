// SPDX-FileCopyrightText: Copyright (C) 2018 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/fdcevvanillaengine.hpp ql/pricingengines/vanilla/fdcevvanillaengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The finite-difference pricing engine for vanilla options, European, American or Bermudan, on the constant elasticity
/// of variance (CEV) process df = α·f^β·dW, absorbed at f = 0: on <see cref="FdmCEV1dMesher"/>'s grid, concentrating
/// at the strike, with the analytic CEV value at the upper end and, where the process can reach zero (δ &lt; 2), the
/// payoff at the lower end discounted from maturity.
/// </summary>
/// <remarks>
/// <para>
/// The engine reports the value, delta, gamma and theta; the theta is <see langword="null"/> when the problem's first
/// stopping time is time 0, as QuantLib's is. Its finite-difference working types are built inside each calculation and
/// dropped after it (the S-eq plan's SEQR1). Its times are the discount curve's, from its reference date in its day
/// counter, as QuantLib's; an exercise on or before that date leaves the engine no time to roll back over, and it is
/// refused, as QuantLib's engine throws there (Boost's refusal of an infinite non-centrality, or a negative time in the
/// incomplete gamma function), and as nanoql's other finite-difference engines refuse an exercise their clocks have no
/// time for.
/// </para>
/// <para>
/// Near β = 1 the mesher's bounds and the upper end's value are the lognormal limit's and Black's formula at Hagan's
/// equivalent volatility (<see cref="FdmCEV1dMesher"/>, <see cref="CEVCalculator"/>; the S-van plan's VR10), where
/// QuantLib's engine throws.
/// </para>
/// </remarks>
public static class FdCEVVanillaEngine
{
    /// <summary>Prices a vanilla option.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="f0">The forward's value today.</param>
    /// <param name="alpha">The volatility parameter α.</param>
    /// <param name="beta">The elasticity β.</param>
    /// <param name="discountCurve">The discount curve.</param>
    /// <param name="tGrid">The number of time steps.</param>
    /// <param name="xGrid">The number of the forward's points.</param>
    /// <param name="dampingSteps">The number of implicit damping steps.</param>
    /// <param name="scalingFactor">The factor the mesher's ends are widened by.</param>
    /// <param name="eps">The probability outside each end of the mesher.</param>
    /// <param name="schemeDesc">The scheme; <see langword="null"/> for Douglas, QuantLib's default.</param>
    /// <returns>The option's results; the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">The option, the context or the curve is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A grid size or the damping steps is negative.</exception>
    /// <exception cref="ArgumentException">
    /// The exercise is on or before the curve's reference date, or the grid is too small.
    /// </exception>
    /// <exception cref="NanoQLException">
    /// The payoff isn't striked, or the density, the analytic value or the solver refuses its arguments.
    /// </exception>
    public static OneAssetOptionResults Calculate(
        VanillaOption option,
        EvaluationContext context,
        double f0,
        double alpha,
        double beta,
        YieldTermStructure discountCurve,
        int tGrid = 50,
        int xGrid = 400,
        int dampingSteps = 0,
        double scalingFactor = 1.0,
        double eps = 1e-4,
        FdmSchemeDesc? schemeDesc = null)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(discountCurve);
        ArgumentOutOfRangeException.ThrowIfNegative(tGrid);
        ArgumentOutOfRangeException.ThrowIfNegative(xGrid);
        ArgumentOutOfRangeException.ThrowIfNegative(dampingSteps);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        OneAssetOptionResults results = Body(
            option,
            context,
            f0,
            alpha,
            beta,
            discountCurve,
            tGrid,
            xGrid,
            dampingSteps,
            scalingFactor,
            eps,
            schemeDesc ?? FdmSchemeDesc.Douglas());
        return option.FetchResults(results);
    }

    internal static OneAssetOptionResults Body(
        VanillaOption option,
        EvaluationContext context,
        double f0,
        double alpha,
        double beta,
        YieldTermStructure rTS,
        int tGrid,
        int xGrid,
        int dampingSteps,
        double scalingFactor,
        double eps,
        FdmSchemeDesc schemeDesc)
    {
        // 1. Mesher
        if (option.Payoff is not StrikedTypePayoff payoff)
        {
            throw new NanoQLException("non-striked payoff given");
        }

        DayCounter dc = rTS.DayCounter;

        Date referenceDate = rTS.ReferenceDate;
        Date maturityDate = option.Exercise.LastDate();
        double maturityTime = dc.YearFraction(referenceDate, maturityDate);

        // QuantLib throws here, in Boost's non-central chi-squared or incomplete gamma
        if (!(maturityTime > 0.0))
        {
            throw new ArgumentException(
                "the exercise is on or before the discount curve's reference date, where the engine has no time",
                nameof(option));
        }

        var cevMesher = new FdmCEV1dMesher(
            xGrid, f0, alpha, beta, maturityTime, eps, scalingFactor, (payoff.Strike, 0.1));

        double lowerBound = cevMesher.Location(0);
        double upperBound = cevMesher.Location(cevMesher.Size - 1);

        var mesher = new FdmMesherComposite(cevMesher);

        // 2. Calculator
        var calculator = new FdmCellAveragingInnerValue(payoff, mesher, 0);

        // 3. Step conditions
        FdmStepConditionComposite conditions = FdmStepConditionComposite.VanillaComposite(
            ImmutableArray<Dividend>.Empty, option.Exercise, mesher, calculator, referenceDate, dc, context);

        // 4. Boundary conditions
        var upperCalculator = new CEVCalculator(upperBound, alpha, beta);
        double UpperBoundPrice(double t)
        {
            // std::max(1/365., maturityTime - t)
            double remaining = maturityTime - t;
            double time2Expiry = (1 / 365.0 < remaining) ? remaining : 1 / 365.0;
            double df = rTS.Discount(maturityTime) / rTS.Discount(t);

            return df * upperCalculator.Value(payoff.OptionType, payoff.Strike, time2Expiry);
        }

        ImmutableArray<BoundaryCondition<FdmLinearOp>>.Builder boundaries =
            ImmutableArray.CreateBuilder<BoundaryCondition<FdmLinearOp>>();
        boundaries.Add(new FdmTimeDepDirichletBoundary(mesher, UpperBoundPrice, 0, BoundaryConditionSide.Upper));

        double delta = (1 - (2 * beta)) / (1 - beta);
        if (delta < 2.0)
        {
            double terminalCashFlow = payoff.Evaluate(lowerBound);

            boundaries.Add(new FdmDiscountDirichletBoundary(
                mesher, rTS, maturityTime, terminalCashFlow, 0, BoundaryConditionSide.Lower));
        }

        // 5. Solver
        var solverDesc = new FdmSolverDesc(
            mesher, boundaries.ToImmutable(), conditions, calculator, maturityTime, tGrid, dampingSteps);

        var op = new FdmCEVOp(mesher, rTS, f0, alpha, beta, 0);

        var solver = new Fdm1DimSolver(solverDesc, schemeDesc, op);

        return new OneAssetOptionResults
        {
            NPV = solver.InterpolateAt(f0),
            Delta = solver.DerivativeX(f0),
            Gamma = solver.DerivativeXX(f0),
            Theta = solver.ThetaAt(f0),
        };
    }
}
