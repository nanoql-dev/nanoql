// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2004 Mike Parker
// SPDX-FileCopyrightText: Copyright (C) 2021 Magnus Mencke
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/shortrate/twofactormodels/g2.hpp ql/models/shortrate/twofactormodels/g2.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Models;

/// <summary>
/// G2's mean-reversion kernels: QuantLib's master's after the pin (#2885, <c>g2.cpp</c>'s <c>expDecayRatio</c> and
/// <c>varianceKernel</c>) and the forms written beside them, which G2 and its processes read.
/// </summary>
internal static class G2Functions
{
    /// <summary>(1 − e^(−x))/x, and 1 at 0.</summary>
    /// <param name="x">The argument.</param>
    /// <returns>(1 − e^(−x))/x.</returns>
    internal static double OneMinusExpOverX(double x) => x == 0.0 ? 1.0 : ExpDecayRatio(x);

    /// <summary>(1 − e^(−x))/x, QuantLib's master's <c>expDecayRatio</c>.</summary>
    /// <param name="x">The argument, not 0.</param>
    /// <returns>(1 − e^(−x))/x.</returns>
    internal static double ExpDecayRatio(double x) => -Expm1Functions.StdExpm1(-x) / x;

    /// <summary>The integral of s²·f(us)·f(vs) over [0, 1], f being <see cref="ExpDecayRatio"/>: QuantLib's master's <c>varianceKernel</c>.</summary>
    /// <param name="u">The first argument.</param>
    /// <param name="v">The second argument.</param>
    /// <returns>The integral.</returns>
    /// <remarks>
    /// Its closed form (1 − f(u) − f(v) + f(u + v))/(uv) subtracts terms that are O(1) to get a result that is O(uv) and
    /// then divides by uv, so it loses all precision when u and v are small and is only used when that is not the
    /// case. G2's V combines this function at (at, at), (bt, bt) and (at, bt).
    /// </remarks>
    internal static double VarianceKernel(double u, double v)
    {
        if (u < v)
        {
            (u, v) = (v, u);
        }

        if (u + v <= 2.0)
        {
            // series sum_(k>=2) (-1)^k*P_k/(k+1)!, where P_k = ((u+v)^k - u^k - v^k)/(u*v),
            // so that P_2 = 2 and P_(k+1) = (u+v)*P_k + u^(k-1) + v^(k-1)
            double s = u + v;
            double P = 2.0, uPower = u, vPower = v, factorial = 6.0, sign = 1.0, sum = 0.0;
            for (int k = 2; k < 60; ++k)
            {
                double term = sign * P / factorial;
                sum += term;
                if (Math.Abs(term) < 0.01 * NumericLimits.QL_EPSILON * Math.Abs(sum))
                {
                    break;
                }

                P = (s * P) + uPower + vPower;
                uPower *= u;
                vPower *= v;
                factorial *= k + 2;
                sign = -sign;
            }

            return sum;
        }

        if (v >= 0.5)
        {
            return (1.0 - ExpDecayRatio(u) - ExpDecayRatio(v) + ExpDecayRatio(u + v)) / (u * v);
        }

        // v is small and u is not. Write 1 - f(v) as v*omega(v), where
        // omega(v) = (v + expm1(-v))/v^2 = 1/2 - v/6 + v^2/24 - ..., and
        // f(u) - f(u+v) = (u*exp(-u)*expm1(-v) - v*expm1(-u))/(u*(u+v))
        double omega = 0.0, t = 0.5;
        for (int k = 2; k < 40; ++k)
        {
            omega += t;
            t *= -v / (k + 1);
            if (Math.Abs(t) < 0.01 * NumericLimits.QL_EPSILON * Math.Abs(omega))
            {
                break;
            }
        }

        double difference =
            ((u * Math.Exp(-u) * Expm1Functions.StdExpm1(-v)) - (v * Expm1Functions.StdExpm1(-u))) / (u * (u + v));
        return ((v * omega) - difference) / (u * v);
    }

    /// <summary>(f(u) − f(u + v))/v, the integral of s·e^(−us)·f(vs) over [0, 1], f being <see cref="OneMinusExpOverX"/>.</summary>
    /// <param name="u">The first argument, positive.</param>
    /// <param name="v">The second argument, positive.</param>
    /// <returns>The integral.</returns>
    /// <remarks>
    /// The swaption function's means and the forward process's M read it. Its closed form,
    /// (1 − e^(−u) − u·e^(−u)·f(v))/(u(u + v)), subtracts terms that are O(u) to get one that is O(u(u + v)), so below
    /// u + v = 2 the series Σ_(k≥0) (−1)ᵏPₖ/((k + 1)!(k + 2)), Pₖ = ((u + v)^(k+1) − u^(k+1))/v, so that P₀ = 1 and
    /// Pₖ = uPₖ₋₁ + (u + v)ᵏ, every term positive.
    /// </remarks>
    internal static double DecayRatioSlope(double u, double v)
    {
        double s = u + v;
        if (s <= 2.0)
        {
            double P = 1.0, sPower = 1.0, denominator = 2.0, sign = 1.0, sum = 0.0;
            for (int k = 0; k < 60; ++k)
            {
                double term = sign * P / denominator;
                sum += term;
                if (Math.Abs(term) < 0.01 * NumericLimits.QL_EPSILON * Math.Abs(sum))
                {
                    break;
                }

                sPower *= s;
                P = (u * P) + sPower;
                denominator *= k + 3;
                sign = -sign;
            }

            return sum;
        }

        return (-Expm1Functions.StdExpm1(-u) - (u * Math.Exp(-u) * OneMinusExpOverX(v))) / (u * s);
    }
}
