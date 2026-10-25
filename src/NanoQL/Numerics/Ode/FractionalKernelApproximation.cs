// SPDX-FileCopyrightText: Copyright (C) 2026 Colin Alberts
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/ode/fractionalkernelapproximation.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>
/// The sum-of-exponentials approximation Kⁿ(t) = Σ cᵢ e^{−xᵢt} of the fractional kernel K(t) = t^{α−1}/Γ(α),
/// α in (0, 1].
/// </summary>
/// <remarks>
/// <para>
/// K is completely monotone, K(t) = ∫₀^∞ e^{−xt} μ(dx) with μ(dx) = x^{−α}/(Γ(α)Γ(1 − α)) dx (Abi Jaber), and a
/// partition 0 = η₀ &lt; η₁ &lt; … &lt; ηₙ gives non-negative cᵢ and xᵢ, so a completely monotone Kⁿ, by collapsing each
/// interval's mass onto its barycentre: cᵢ = ∫ μ(dx) and xᵢ = (1/cᵢ) ∫ x μ(dx) over [ηᵢ₋₁, ηᵢ]. With η₀ = 0 the first
/// interval absorbs all the mass as α rises to 1, so c₁ → 1, x₁ → 0 and Kⁿ → K ≡ 1. The partition's ends widen with n,
/// η₁ = 1/(√n t_max) and ηₙ = 4n^{5/2}/t_max, their exponents QuantLib's empirical ones.
/// </para>
/// <para>
/// Immutable and thread-safe.
/// </para>
/// <para>
/// References: E. Abi Jaber, Lifting the Heston model, Quantitative Finance 19(12), 1995-2013 (2019); E. Abi Jaber and
/// O. El Euch, Multifactor approximation of rough volatility models, SIAM Journal on Financial Mathematics 10(2),
/// 309-349 (2019).
/// </para>
/// </remarks>
public sealed class FractionalKernelApproximation
{
    /// <summary>Initializes a new instance of the <see cref="FractionalKernelApproximation"/> class.</summary>
    /// <param name="alpha">The fractional order, in (0, 1].</param>
    /// <param name="n">The number of factors.</param>
    /// <param name="tMax">The horizon the partition is sized for.</param>
    /// <exception cref="ArgumentException"><paramref name="tMax"/> or <paramref name="n"/> isn't positive, or
    /// <paramref name="alpha"/> isn't in (0, 1].</exception>
    public FractionalKernelApproximation(double alpha, int n, double tMax)
    {
        if (!(tMax > 0.0))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"tMax ({tMax}) must be positive"), nameof(tMax));
        }

        if (!(n > 0))
        {
            throw new ArgumentException("at least one factor required", nameof(n));
        }

        (Weights, Rates) = Build(alpha, n, 1.0 / (Math.Sqrt(n) * tMax), 4.0 * Math.Pow(n, 2.5) / tMax);
    }

    /// <summary>Gets the weights cᵢ.</summary>
    public ImmutableArray<double> Weights { get; }

    /// <summary>Gets the mean-reversion rates xᵢ.</summary>
    public ImmutableArray<double> Rates { get; }

    /// <summary>Gets the number of factors.</summary>
    public int Size => Weights.Length;

    /// <summary>The approximation Kⁿ(t) = Σ cᵢ e^{−xᵢt}.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The kernel's approximation at <paramref name="t"/>.</returns>
    public double Value(double t)
    {
        double value = 0.0;
        for (int i = 0; i < Weights.Length; ++i)
        {
            value += Weights[i] * Math.Exp(-Rates[i] * t);
        }

        return value;
    }

    // (exp(y) - 1) / y, continuous at the origin
    private static double Expm1OverX(double y)
    {
        if (Math.Abs(y) < 1e-8)
        {
            return 1.0 + (0.5 * y);
        }

        return Expm1Functions.StdExpm1(y) / y;
    }

    // sin(pi b) / (pi b), the reflection-formula rewriting of 1 / ((1 - alpha) Gamma(alpha) Gamma(1 - alpha)) for
    // b = 1 - alpha: Gamma(1 - alpha) overflows as alpha -> 1, whereas this form tends to one
    private static double FractionalKernelNorm(double b)
    {
        double y = Math.PI * b;
        if (Math.Abs(y) < 1e-8)
        {
            return 1.0 - (y * y / 6.0);
        }

        return Math.Sin(y) / y;
    }

    private static (ImmutableArray<double> C, ImmutableArray<double> X) Build(double alpha, int n, double etaLo, double etaHi)
    {
        if (!(alpha > 0.0 && alpha <= 1.0))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"fractional order alpha ({alpha}) must be in (0, 1]"),
                nameof(alpha));
        }

        if (!(etaLo > 0.0 && etaHi >= etaLo))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"partition endpoints ({etaLo}, {etaHi}) must satisfy 0 < etaLo <= etaHi"),
                "tMax");
        }

        double b = 1.0 - alpha;
        double norm = FractionalKernelNorm(b);
        var c = new double[n];
        var x = new double[n];

        // A one-factor partition has to put its single node at the fast end,
        // so that the whole mass still sits on [0, eta_1]
        double eta1 = (n == 1) ? etaHi : etaLo;

        // The first interval is [0, eta_1], whose mass tends to the total
        // mass as alpha -> 1; this is what recovers the constant kernel
        c[0] = Math.Pow(eta1, b) * norm;
        x[0] = b / (1.0 + b) * eta1;

        if (n > 1)
        {
            double dL = Math.Log(etaHi / etaLo) / (n - 1);
            double r = Math.Exp(dL);

            // Both weight and rate go through expm1, so that the differences
            // eta_i^b - eta_{i-1}^b, which suffer catastrophic cancellation as
            // alpha -> 1, are never formed explicitly
            double weightFactor = Expm1Functions.StdExpm1(b * dL) * norm;
            double rateFactor = Expm1OverX((1.0 + b) * dL) / Expm1OverX(b * dL);

            double eta = eta1;
            for (int i = 1; i < n; ++i)
            {
                c[i] = Math.Pow(eta, b) * weightFactor;
                x[i] = eta * rateFactor;
                eta *= r;
            }
        }

        return (ImmutableArray.Create(c), ImmutableArray.Create(x));
    }
}
