// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Numerics;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// An independent value of a two-asset correlation option on flat continuous markets: the discounted expectation of
/// the second asset's payoff where the first is in the money, integrated over the second asset's driving normal, each
/// first-asset probability conditional on it in closed form, the payoff's kink an end of the integral; Gauss-Lobatto
/// at an absolute accuracy of 1e-13.
/// </summary>
internal static class TwoAssetCorrelationQuadrature
{
    private static readonly NormalDistribution Phi = new();
    private static readonly CumulativeNormalDistribution N = new();

    internal static double Value(
        OptionType type,
        double x1,
        double x2,
        double s1,
        double s2,
        double q1,
        double q2,
        double r,
        double sigma1,
        double sigma2,
        double rho,
        double t)
    {
        double f1 = s1 * Math.Exp((r - q1) * t), f2 = s2 * Math.Exp((r - q2) * t);
        double sq = Math.Sqrt(t);
        double cp = type == OptionType.Call ? 1.0 : -1.0;

        // conditional on z2, ln S1 is normal: mean ln F1 - σ1²T/2 + ρσ1√T z2, deviation σ1√T√(1 - ρ²)
        double Integrand(double z2)
        {
            double bigS2 = f2 * Math.Exp((-0.5 * sigma2 * sigma2 * t) + (sigma2 * sq * z2));
            double payoff = cp * (bigS2 - x2);
            if (payoff <= 0.0)
            {
                return 0.0;
            }

            double d = (Math.Log(f1 / x1) - (0.5 * sigma1 * sigma1 * t) + (rho * sigma1 * sq * z2))
                / (sigma1 * sq * Math.Sqrt(1.0 - (rho * rho)));
            return Phi.Evaluate(z2) * payoff * N.Evaluate(cp * d);
        }

        // the payoff's kink, where S2 = X2, ends the integral; none for X2 <= 0
        double lower = -12.0, upper = 12.0;
        if (x2 > 0.0)
        {
            double kink = (Math.Log(x2 / f2) + (0.5 * sigma2 * sigma2 * t)) / (sigma2 * sq);
            if (type == OptionType.Call)
            {
                lower = kink;
            }
            else
            {
                upper = kink;
            }
        }

        var integrator = new GaussLobattoIntegral(100000, 1e-13);
        return Math.Exp(-r * t) * integrator.Integrate(Integrand, lower, upper);
    }
}
