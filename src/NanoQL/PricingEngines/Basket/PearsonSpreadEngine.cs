// SPDX-FileCopyrightText: Copyright (C) 2026 Yassine Idyiahia
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/basket/pearsonspreadengine.hpp ql/pricingengines/basket/pearsonspreadengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Instruments;
using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.PricingEngines;

/// <summary>
/// Pearson's engine for a European spread option on two futures: the Black price conditional on the second asset,
/// integrated over its driving normal by Gauss-Lobatto quadrature; N. D. Pearson, "An Efficient Approach for Pricing
/// Spread Options", Journal of Derivatives 3 (1995), 76-91.
/// </summary>
/// <remarks>
/// Immutable. Where the conditional strike is zero or less a put's conditional value is 0, where QuantLib takes the
/// call's intrinsic value whatever the type (<c>Divergences/PearsonNegativeStrikePutTests.cs</c>).
/// </remarks>
public sealed class PearsonSpreadEngine : SpreadBlackScholesVanillaEngine
{
    private static readonly NormalDistribution Phi = new();

    private readonly double integrationTolerance_;
    private readonly int maxIntegrationIterations_;
    private readonly double nStd_;

    /// <summary>Initializes a new instance of the <see cref="PearsonSpreadEngine"/> class.</summary>
    /// <param name="process1">The first asset's process.</param>
    /// <param name="process2">The second asset's process.</param>
    /// <param name="correlation">The correlation of the two assets.</param>
    /// <param name="integrationTolerance">The quadrature's absolute accuracy.</param>
    /// <param name="maxIntegrationIterations">The quadrature's iteration limit.</param>
    /// <param name="nStd">The standard deviations either side of zero the integral spans.</param>
    /// <exception cref="ArgumentNullException">A process is <see langword="null"/>.</exception>
    public PearsonSpreadEngine(
        GeneralizedBlackScholesProcess process1,
        GeneralizedBlackScholesProcess process2,
        double correlation,
        double integrationTolerance = 1e-10,
        int maxIntegrationIterations = 10000,
        double nStd = 8.0)
        : base(process1, process2, correlation)
    {
        integrationTolerance_ = integrationTolerance;
        maxIntegrationIterations_ = maxIntegrationIterations;
        nStd_ = nStd;
    }

    /// <summary>Prices a European spread option by Pearson's integral.</summary>
    /// <param name="option">The option, on a <see cref="SpreadBasketPayoff"/> over a plain vanilla payoff.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="process1">The first asset's process.</param>
    /// <param name="process2">The second asset's process.</param>
    /// <param name="correlation">The correlation of the two assets.</param>
    /// <param name="integrationTolerance">The quadrature's absolute accuracy.</param>
    /// <param name="maxIntegrationIterations">The quadrature's iteration limit.</param>
    /// <param name="nStd">The standard deviations either side of zero the integral spans.</param>
    /// <returns>The option's results: the value; the expired results when it has expired.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">
    /// The exercise isn't European, the payoff isn't a spread payoff over a plain vanilla payoff, or the quadrature
    /// doesn't converge.
    /// </exception>
    public static MultiAssetOptionResults Calculate(
        BasketOption option,
        EvaluationContext context,
        GeneralizedBlackScholesProcess process1,
        GeneralizedBlackScholesProcess process2,
        double correlation,
        double integrationTolerance = 1e-10,
        int maxIntegrationIterations = 10000,
        double nStd = 8.0) =>
        new PearsonSpreadEngine(
            process1, process2, correlation, integrationTolerance, maxIntegrationIterations, nStd)
            .Calculate(option, context);

    /// <inheritdoc/>
    protected override double Calculate(
        double f1, double f2, double strike, OptionType optionType, double variance1, double variance2, double df)
    {
        double sigma1 = Math.Sqrt(variance1);
        double sigma2 = Math.Sqrt(variance2);

        // Under the forward measure, the joint dynamics are:
        //   ln F1 ~ N(mu1, sigma1^2),  ln F2 ~ N(mu2, sigma2^2)
        //   with correlation rho_
        //
        // Condition on z = standard normal driving F2:
        //   F2(z) = f2 * exp(-0.5*sigma2^2 + sigma2*z)
        //
        // Conditional on z, F1 is log-normal with:
        //   conditional mean of ln F1:  mu1_cond = ln(f1) - 0.5*sigma1^2 + rho_*sigma1*z
        //   conditional variance:       sigma1_cond^2 = sigma1^2*(1 - rho_^2)
        //
        // The conditional spread option value is a Black call/put:
        //   BS(F1_cond, K + F2(z), sigma1_cond)
        double oneMinusRho2 = 1.0 - (Rho * Rho);
        double sigma1Cond = sigma1 * Math.Sqrt(0.0 < oneMinusRho2 ? oneMinusRho2 : 0.0);

        // Integrate over the standard normal variable z
        // that drives F2. Truncate at +/- 8 standard deviations.
        double Integrand(double z)
        {
            double f2z = f2 * Math.Exp((-0.5 * variance2) + (sigma2 * z));
            double effectiveStrike = f2z + strike;

            if (effectiveStrike <= 0.0)
            {
                // the put pays nothing at a strike of zero or less; the call its forward less the strike
                if (optionType == OptionType.Put)
                {
                    return 0.0;
                }

                double intrinsic = (f1 * Math.Exp((Rho * sigma1 * z) - (0.5 * Rho * Rho * variance1))) -
                    effectiveStrike;
                return Phi.Evaluate(z) * (0.0 < intrinsic ? intrinsic : 0.0);
            }

            double f1Cond = f1 * Math.Exp((Rho * sigma1 * z) - (0.5 * Rho * Rho * variance1));

            var black = new BlackCalculator(optionType, effectiveStrike, f1Cond, sigma1Cond, 1.0);

            return Phi.Evaluate(z) * black.Value;
        }

        var integrator = new GaussLobattoIntegral(maxIntegrationIterations_, integrationTolerance_);

        double undiscounted = integrator.Integrate(Integrand, -nStd_, nStd_);

        return df * undiscounted;
    }
}
