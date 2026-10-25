// SPDX-FileCopyrightText: Copyright (C) 2019 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/fdsabrvanillaengine.hpp ql/pricingengines/vanilla/fdsabrvanillaengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Coupons;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.PricingEngines;

/// <summary>
/// The finite-difference pricing engine for vanilla options, European, American or Bermudan, on the SABR model
/// dF = α·F^β·dW₁, dα = ν·α·dW₂, d⟨W₁, W₂⟩ = ρ·dt: on <see cref="FdmCEV1dMesher"/>'s grid of the forward, at the
/// volatility's upper quartile, by a grid of the log volatility over ε-quantiles, with the payoff at both ends of the
/// forward discounted from maturity.
/// </summary>
/// <remarks>
/// <para>
/// The engine reports the value. Its finite-difference working types are built inside each calculation and dropped
/// after it (the S-eq plan's SEQR1). Its times are the discount curve's, from its reference date in its day counter,
/// as QuantLib's; an exercise on or before that date is refused, as <see cref="FdCEVVanillaEngine"/> refuses it, where
/// QuantLib's engine throws.
/// </para>
/// <para>
/// At ν = 0, which QuantLib's parameter check accepts, the log volatility's grid collapses and QuantLib's engine throws
/// ("end must be larger than start"); so it does at a tiny positive ν, where the grid's span is a few ulps of ln α or
/// none, and a little above it returns NaN (ν about 1e-15 over a year). SABR at ν = 0 is the CEV process with α its
/// volatility, and wherever ν√T is at most 1e-10·max(1, |ln α|) the engine prices on <see cref="FdCEVVanillaEngine"/>
/// with its own grids (the forward's for the CEV's), damping steps, scaling, ε and scheme (the S-van plan's VR4 (xxxi),
/// VD28): from there up QuantLib's grid holds, and its values are the CEV's up to the two grids' difference.
/// </para>
/// </remarks>
public static class FdSabrVanillaEngine
{
    // ν√T at or below this, relative to max(1, |ln α|), prices on the CEV engine: the log volatility's grid is then
    // at most about 4e-10 wide, far above the few ulps of ln α where it collapses
    private const double NegligibleVolOfVol = 1e-10;

    /// <summary>Prices a vanilla option.</summary>
    /// <param name="option">The option.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="f0">The forward's value today.</param>
    /// <param name="alpha">The initial volatility α, positive.</param>
    /// <param name="beta">The elasticity β, below 1.</param>
    /// <param name="nu">The volatility of volatility ν, non-negative.</param>
    /// <param name="rho">The correlation ρ, in (−1, 1).</param>
    /// <param name="rTS">The discount curve.</param>
    /// <param name="tGrid">The number of time steps.</param>
    /// <param name="fGrid">The number of the forward's points.</param>
    /// <param name="xGrid">The number of the log volatility's points.</param>
    /// <param name="dampingSteps">The number of implicit damping steps.</param>
    /// <param name="scalingFactor">The factor the grids' ends are widened by.</param>
    /// <param name="eps">The probability outside each end of the grids.</param>
    /// <param name="schemeDesc">The scheme; <see langword="null"/> for Hundsdorfer, QuantLib's default.</param>
    /// <returns>The option's results: the value; the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">The option, the context or the curve is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">A grid size or the damping steps is negative.</exception>
    /// <exception cref="ArgumentException">
    /// A parameter is out of its range, with QuantLib's messages; the exercise is on or before the curve's reference
    /// date; or a grid is too small.
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
        double nu,
        double rho,
        YieldTermStructure rTS,
        int tGrid = 50,
        int fGrid = 400,
        int xGrid = 50,
        int dampingSteps = 0,
        double scalingFactor = 1.0,
        double eps = 1e-4,
        FdmSchemeDesc? schemeDesc = null)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(rTS);

        // QuantLib's constructor's checks
        SabrFunctions.ValidateSabrParameters(alpha, 0.5, nu, rho);

        if (!(beta < 1.0))
        {
            throw new ArgumentException(
                $"beta must be smaller than 1.0: {beta.ToString(CultureInfo.InvariantCulture)} not allowed",
                nameof(beta));
        }

        ArgumentOutOfRangeException.ThrowIfNegative(tGrid);
        ArgumentOutOfRangeException.ThrowIfNegative(fGrid);
        ArgumentOutOfRangeException.ThrowIfNegative(xGrid);
        ArgumentOutOfRangeException.ThrowIfNegative(dampingSteps);
        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        FdmSchemeDesc scheme = schemeDesc ?? FdmSchemeDesc.Hundsdorfer();

        OneAssetOptionResults results = Body(
            option, context, f0, alpha, beta, nu, rho, rTS, tGrid, fGrid, xGrid, dampingSteps, scalingFactor, eps, scheme);

        return option.FetchResults(results);
    }

    private static OneAssetOptionResults Body(
        VanillaOption option,
        EvaluationContext context,
        double f0,
        double alpha,
        double beta,
        double nu,
        double rho,
        YieldTermStructure rTS,
        int tGrid,
        int fGrid,
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

        // QuantLib throws here, in Boost's non-central chi-squared or the mesher
        if (!(maturityTime > 0.0))
        {
            throw new ArgumentException(
                "the exercise is on or before the discount curve's reference date, where the engine has no time",
                nameof(option));
        }

        // SABR with a negligible volatility of volatility is CEV (VR4 (xxxi), VD28)
        if (!(nu * Math.Sqrt(maturityTime) > NegligibleVolOfVol * Math.Max(1.0, Math.Abs(Math.Log(alpha)))))
        {
            OneAssetOptionResults cev = FdCEVVanillaEngine.Body(
                option, context, f0, alpha, beta, rTS, tGrid, fGrid, dampingSteps, scalingFactor, eps, schemeDesc);
            return new OneAssetOptionResults { NPV = cev.NPV };
        }

        double upperAlpha = alpha * Math.Exp(nu * Math.Sqrt(maturityTime) * new InverseCumulativeNormal().Evaluate(0.75));

        var cevMesher = new FdmCEV1dMesher(
            fGrid, f0, upperAlpha, beta, maturityTime, eps, scalingFactor, (payoff.Strike, 0.025));

        double normInvEps = new InverseCumulativeNormal().Evaluate(1 - eps);
        double logDrift = -0.5 * nu * nu * maturityTime;
        double volRange = nu * Math.Sqrt(maturityTime) * normInvEps * scalingFactor;

        double xMin = Math.Log(alpha) + logDrift - volRange;
        double xMax = Math.Log(alpha) + logDrift + volRange;

        var xMesher = new Concentrating1dMesher(xMin, xMax, xGrid, (Math.Log(alpha), 0.1));

        var mesher = new FdmMesherComposite(cevMesher, xMesher);

        // 2. Calculator
        var calculator = new FdmCellAveragingInnerValue(payoff, mesher, 0);

        // 3. Step conditions
        FdmStepConditionComposite conditions = FdmStepConditionComposite.VanillaComposite(
            ImmutableArray<Dividend>.Empty, option.Exercise, mesher, calculator, referenceDate, dc, context);

        // 4. Boundary conditions
        double lowerBound = cevMesher.Location(0);
        double upperBound = cevMesher.Location(cevMesher.Size - 1);

        ImmutableArray<BoundaryCondition<FdmLinearOp>> boundaries =
        [
            new FdmDiscountDirichletBoundary(
                mesher, rTS, maturityTime, payoff.Evaluate(upperBound), 0, BoundaryConditionSide.Upper),
            new FdmDiscountDirichletBoundary(
                mesher, rTS, maturityTime, payoff.Evaluate(lowerBound), 0, BoundaryConditionSide.Lower),
        ];

        // 5. Solver
        var solverDesc = new FdmSolverDesc(mesher, boundaries, conditions, calculator, maturityTime, tGrid, dampingSteps);

        var op = new FdmSabrOp(mesher, rTS, f0, alpha, beta, nu, rho);

        var solver = new Fdm2DimSolver(solverDesc, schemeDesc, op);

        return new OneAssetOptionResults { NPV = solver.InterpolateAt(f0, Math.Log(alpha)) };
    }
}
