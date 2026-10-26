// SPDX-FileCopyrightText: Copyright (C) 2026 Yassine Idyiahia
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/basket/gaussiancopulaspreadengine.hpp ql/pricingengines/basket/gaussiancopulaspreadengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;
using NanoQL.Instruments;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.NumericLimits;

namespace NanoQL.PricingEngines;

/// <summary>
/// The Gaussian copula engine for a European spread option, max(S₁ − S₂ − K, 0) and its put, with smile-implied
/// marginals: nested Gauss-Hermite quadrature over a Gaussian copula of the two risk-neutral densities.
/// </summary>
/// <remarks>
/// <para>
/// Each marginal is <see cref="SmileSectionRNDCalculator"/>'s on the volatility's smile section at expiry, centred
/// on its forward (<see cref="AtmSmileSection"/>). The two processes must share their risk-free curve, the same
/// object, as QuantLib requires the same handle link; the payoff is discounted on it.
/// </para>
/// <para>
/// Each curve is read by date and accrues nothing before its reference date, where QuantLib's reads throw for an
/// option expiring before a later structure's date (the playbook's dates row,
/// <c>Divergences/TwoAssetMismatchedTimesTests.cs</c>). An asset whose volatility accrues no variance to expiry, an
/// expiry on or before the volatility's date or a zero volatility, is at its forward there, where QuantLib's density
/// calculator refuses ("non-positive at-the-money volatility", <c>Divergences/GaussianCopulaZeroVarianceTests.cs</c>);
/// a variance within rounding of zero keeps QuantLib's refusal through Black's formula's check of a negative value.
/// </para>
/// </remarks>
public static class GaussianCopulaSpreadEngine
{
    private static readonly CumulativeNormalDistribution Phi = new();

    /// <summary>Prices a European spread option by the Gaussian copula: QuantLib's <c>calculate</c>.</summary>
    /// <param name="option">The option, on a <see cref="SpreadBasketPayoff"/> over a plain vanilla payoff.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process1">The first asset's process.</param>
    /// <param name="process2">The second asset's process, on the first's risk-free curve.</param>
    /// <param name="correlation">The copula's correlation, in [−1, 1].</param>
    /// <param name="nPoints">The Gauss-Hermite order in each dimension.</param>
    /// <returns>The option's results: the value; the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The correlation is outside [−1, 1], or the processes' risk-free curves aren't the same object.
    /// </exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't European, or the payoff isn't a spread payoff over a plain vanilla payoff.
    /// </exception>
    public static MultiAssetOptionResults Calculate(
        BasketOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process1,
        GeneralizedBlackScholesProcess process2,
        double correlation,
        int nPoints = 64)
    {
        ArgumentNullException.ThrowIfNull(option);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(process1);
        ArgumentNullException.ThrowIfNull(process2);

        // QuantLib's constructor
        if (!(correlation >= -1.0 && correlation <= 1.0))
        {
            throw new ArgumentException(
                $"correlation must be in [-1, 1], got {correlation.ToString(CultureInfo.InvariantCulture)}",
                nameof(correlation));
        }

        if (!ReferenceEquals(process1.RiskFreeRate, process2.RiskFreeRate))
        {
            throw new ArgumentException(
                "process1 and process2 must share the risk-free term structure (used for discounting the spread payoff)",
                nameof(process2));
        }

        if (option.IsExpired(context))
        {
            return option.SetupExpired();
        }

        option.Validate();
        return option.FetchResults(new MultiAssetOptionResults
        {
            NPV = Value(option, process1, process2, correlation, nPoints),
        });
    }

    // QuantLib's calculate().
    private static double Value(
        BasketOption option,
        GeneralizedBlackScholesProcess process1,
        GeneralizedBlackScholesProcess process2,
        double rho,
        int nPoints)
    {
        if (option.Exercise is not EuropeanExercise exercise)
        {
            throw new NanoQLException("not a European exercise");
        }

        if (option.Payoff is not SpreadBasketPayoff spreadPayoff)
        {
            throw new NanoQLException("spread payoff expected");
        }

        if (spreadPayoff.BasePayoff is not PlainVanillaPayoff payoff)
        {
            throw new NanoQLException("non-plain payoff given");
        }

        // 1. Forwards, discount, and smile sections.
        Date maturityDate = exercise.LastDate();
        double fwd1 = process1.X0
            * process1.DividendYield.AccruedDiscount(maturityDate)
            / process1.RiskFreeRate.AccruedDiscount(maturityDate);
        double fwd2 = process2.X0
            * process2.DividendYield.AccruedDiscount(maturityDate)
            / process2.RiskFreeRate.AccruedDiscount(maturityDate);
        double df = process1.RiskFreeRate.AccruedDiscount(maturityDate);

        // 2. Risk-neutral marginals: each asset's level at a probability.
        Func<double, double> spot1 = Marginal(process1.BlackVolatility, maturityDate, fwd1);
        Func<double, double> spot2 = Marginal(process2.BlackVolatility, maturityDate, fwd2);

        // 3. Nested Gauss-Hermite quadrature.  GaussHermiteIntegration
        // approximates int f(x) dx, so to integrate against phi(z) we
        // substitute z = sqrt(2)*x and weight by (1/pi) exp(-x_i^2 - x_j^2).
        var gh = new GaussHermiteIntegration(nPoints);
        Vector x = gh.X();
        Vector w = gh.Weights();
        double oneMinusRho2 = 1.0 - (rho * rho);
        double rhoComp = Math.Sqrt(0.0 < oneMinusRho2 ? oneMinusRho2 : 0.0);
        double normFactor = 1.0 / Math.PI;

        double sum = 0.0;
        for (int i = 0; i < gh.Order; ++i)
        {
            double z1 = MathConstants.M_SQRT2 * x[i];

            // At extreme GH nodes Phi(z) saturates to exactly 0 or 1;
            // nudge it inside the open interval that invcdf requires.
            double u1 = Math.Clamp(Phi.Evaluate(z1), QL_EPSILON, 1.0 - QL_EPSILON);
            double s1 = spot1(u1);
            double expX1sq = Math.Exp(-x[i] * x[i]);

            double innerSum = 0.0;
            for (int j = 0; j < gh.Order; ++j)
            {
                double z2perp = MathConstants.M_SQRT2 * x[j];
                double z2 = (rho * z1) + (rhoComp * z2perp);
                double u2 = Math.Clamp(Phi.Evaluate(z2), QL_EPSILON, 1.0 - QL_EPSILON);
                double s2 = spot2(u2);

                double payoffVal = payoff.Evaluate(s1 - s2);
                double kernel = expX1sq * Math.Exp(-x[j] * x[j]);
                innerSum += w[j] * kernel * payoffVal;
            }

            sum += w[i] * innerSum;
        }

        return df * normFactor * sum;
    }

    // The asset's level at probability u: exp of the smile's risk-neutral quantile at expiry, QuantLib's; its forward
    // where the volatility accrues no variance to expiry (an expiry before its reference date, or a smile with no
    // deviation at the forward, the density calculator's own test), where QuantLib's reads throw or its density
    // calculator refuses.
    private static Func<double, double> Marginal(BlackVolTermStructure volatility, Date maturityDate, double fwd)
    {
        if (maturityDate < volatility.ReferenceDate)
        {
            return _ => fwd;
        }

        double t = volatility.TimeFromReference(maturityDate);
        var smile = new AtmSmileSection(volatility.SmileSection(t), fwd);
        if (smile.Volatility(fwd) * Math.Sqrt(t) == 0.0)
        {
            return _ => fwd;
        }

        var rnd = new SmileSectionRNDCalculator(smile);
        return u => Math.Exp(rnd.InvCdf(u));
    }
}
