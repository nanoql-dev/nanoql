// SPDX-FileCopyrightText: Copyright (C) 2023 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/expsinhintegral.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The exp-sinh quadrature of Boost.Math, a rapidly convergent integration scheme for holomorphic integrands on
/// half-infinite intervals. The tolerance is used against the error estimate for the L1 norm of the integral.
/// </summary>
/// <remarks>
/// QuantLib's branch for Boost 1.69 and later, the pinned Boost being 1.90.0. <see cref="Integrate(Func{double, double})"/>
/// integrates over [0, ∞), and <see cref="Integrator.Integrate(Func{double, double}, double, double)"/> over [a, ∞)
/// with b at <see cref="double.MaxValue"/> or above, or over (−∞, b] with a at −<see cref="double.MaxValue"/> or below;
/// two finite bounds are refused with Boost's message, as two infinite ones. Its absolute accuracy and maximum number
/// of evaluations are QuantLib's <c>QL_MAX_REAL</c> and <c>Null&lt;Size&gt;()</c>, which nothing reads; the error
/// estimate QuantLib sets is <see cref="IntegrationResult.AbsoluteError"/>, Boost's.
/// </remarks>
public sealed class ExpSinhIntegral : Integrator
{
    private readonly double relTolerance;
    private readonly ExpSinh expSinh;

    /// <summary>Initializes a new instance of the <see cref="ExpSinhIntegral"/> class.</summary>
    /// <param name="relTolerance">The tolerance relative to the L1 norm; QuantLib's default is √ε.</param>
    /// <param name="maxRefinements">The most refinements; fewer than Boost's seven seeded ones count as
    /// seven.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxRefinements"/> is negative.</exception>
    public ExpSinhIntegral(double relTolerance = 1.4901161193847656E-08, int maxRefinements = 9)
        : base(NumericLimits.QL_MAX_REAL, int.MaxValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxRefinements);
        this.relTolerance = relTolerance;
        expSinh = new ExpSinh(maxRefinements);
    }

    /// <summary>The integral of <paramref name="f"/> over [0, ∞).</summary>
    /// <param name="f">The integrand.</param>
    /// <returns>The integral.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="f"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The tolerance is above 1 in magnitude.</exception>
    /// <exception cref="NanoQLException">The integral of |f| isn't finite.</exception>
    public double Integrate(Func<double, double> f) => IntegrateWithStatistics(f).Value;

    /// <summary>
    /// The integral of <paramref name="f"/> over [0, ∞), with the number of evaluations and Boost's error estimate.
    /// </summary>
    /// <param name="f">The integrand.</param>
    /// <returns>The integral, its evaluations and its error estimate.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="f"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The tolerance is above 1 in magnitude.</exception>
    /// <exception cref="NanoQLException">The integral of |f| isn't finite.</exception>
    public IntegrationResult IntegrateWithStatistics(Func<double, double> f)
    {
        ArgumentNullException.ThrowIfNull(f);
        int evaluations = 0;
        double value = expSinh.Integrate(
            x =>
            {
                ++evaluations;
                return f(x);
            },
            relTolerance,
            out double error,
            out _,
            out _);
        return new IntegrationResult(value, evaluations) { AbsoluteError = error };
    }

    /// <inheritdoc/>
    protected override IntegrationResult IntegrateCore(Func<double, double> f, double a, double b)
    {
        int evaluations = 0;
        double value = expSinh.Integrate(
            x =>
            {
                ++evaluations;
                return f(x);
            },
            a,
            b,
            relTolerance,
            out double error,
            out _,
            out _);
        return new IntegrationResult(value, evaluations) { AbsoluteError = error };
    }
}
