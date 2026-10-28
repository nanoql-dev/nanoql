// SPDX-FileCopyrightText: Copyright (C) 2012 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/richardsonextrapolation.hpp ql/math/richardsonextrapolation.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// Richardson extrapolation, a sequence acceleration for f(Δh) = f₀ + α·(Δh)ⁿ + O((Δh)ⁿ⁺¹): f extrapolated to Δh → 0
/// from its values at Δh and at Δh scaled down.
/// </summary>
/// <remarks>Immutable: f(Δh) is evaluated once, at construction, as QuantLib's.</remarks>
public sealed class RichardsonExtrapolation
{
    private readonly double deltaH;
    private readonly double fDeltaH;
    private readonly double? n;
    private readonly Func<double, double> f;

    /// <summary>Initializes a new instance of the <see cref="RichardsonExtrapolation"/> class.</summary>
    /// <param name="f">The function to extrapolate to Δh → 0.</param>
    /// <param name="deltaH">The step size Δh.</param>
    /// <param name="n">The order of convergence, if known; <see langword="null"/> (QuantLib's <c>Null</c>) if
    /// not.</param>
    /// <exception cref="ArgumentNullException"><paramref name="f"/> is null.</exception>
    public RichardsonExtrapolation(Func<double, double> f, double deltaH, double? n = null)
    {
        ArgumentNullException.ThrowIfNull(f);
        this.deltaH = deltaH;
        fDeltaH = f(deltaH);
        this.n = n;
        this.f = f;
    }

    /// <summary>The extrapolation for the known order of convergence.</summary>
    /// <param name="t">The scaling factor of the step size, above 1.</param>
    /// <returns>(tⁿ·f(Δh/t) − f(Δh))/(tⁿ − 1).</returns>
    /// <exception cref="ArgumentException"><paramref name="t"/> isn't above 1.</exception>
    /// <exception cref="NanoQLException">The order of convergence isn't known.</exception>
    public double Evaluate(double t = 2.0)
    {
        if (!(t > 1))
        {
            throw new ArgumentException("scaling factor must be greater than 1", nameof(t));
        }

        if (n is not double order)
        {
            throw new NanoQLException("order of convergence must be known");
        }

        double tk = Math.Pow(t, order);

        return ((tk * f(deltaH / t)) - fDeltaH) / (tk - 1.0);
    }

    /// <summary>The extrapolation for an unknown order of convergence, estimated from two scalings.</summary>
    /// <param name="t">The first scaling factor of the step size, above <paramref name="s"/>.</param>
    /// <param name="s">The second scaling factor of the step size, above 1.</param>
    /// <returns>The extrapolated value.</returns>
    /// <exception cref="ArgumentException">A factor isn't above 1, or <paramref name="t"/> isn't above
    /// <paramref name="s"/>.</exception>
    /// <exception cref="NanoQLException">The order of convergence can't be estimated.</exception>
    public double Evaluate(double t, double s)
    {
        if (!(t > 1 && s > 1))
        {
            throw new ArgumentException("scaling factors must be greater than 1", nameof(t));
        }

        if (!(t > s))
        {
            throw new ArgumentException("t must be greater than s", nameof(t));
        }

        double ft = f(deltaH / t);
        double fs = f(deltaH / s);

        double Eqn(double k) =>
            ft + ((ft - fDeltaH) / (Math.Pow(t, k) - 1.0)) - (fs + ((fs - fDeltaH) / (Math.Pow(s, k) - 1.0)));

        const double step = 0.1;
        double left = 0.05;
        double fr = Eqn(left + step), fl = Eqn(left);
        while (fr * fl > 0.0 && left < 15.1)
        {
            left += step;
            fl = fr;
            fr = Eqn(left + step);
        }

        if (!(left < 15.1))
        {
            throw new NanoQLException("could not estimate the order of convergence");
        }

        double k = new Brent().Solve(Eqn, 1e-8, left + (0.5 * step), left, left + step);

        double ts = Math.Pow(s, k);

        return ((ts * fs) - fDeltaH) / (ts - 1.0);
    }
}
