// SPDX-FileCopyrightText: Copyright (C) 2021 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/tanhsinhintegral.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The tanh-sinh quadrature of Boost.Math, a rapidly convergent integration scheme for holomorphic integrands. The
/// tolerance is used against the error estimate for the L1 norm of the integral.
/// </summary>
/// <remarks>
/// QuantLib's branch for Boost 1.69 and later, the pinned Boost being 1.90.0. Its absolute accuracy and maximum number
/// of evaluations are QuantLib's <c>QL_MAX_REAL</c> and <c>Null&lt;Size&gt;()</c>, which nothing reads; the error estimate
/// QuantLib sets is <see cref="IntegrationResult.AbsoluteError"/>, Boost's.
/// </remarks>
public sealed class TanhSinhIntegral : Integrator
{
    private readonly double relTolerance;
    private readonly TanhSinh tanhSinh;

    /// <summary>Initializes a new instance of the <see cref="TanhSinhIntegral"/> class.</summary>
    /// <param name="relTolerance">The tolerance relative to the L1 norm; QuantLib's default is √ε.</param>
    /// <param name="maxRefinements">The most refinements; fewer than Boost's seven seeded ones count as
    /// seven.</param>
    /// <param name="minComplement">The smallest complement of an abscissa to ±1 the quadrature keeps; QuantLib's
    /// default is 4·<c>DBL_MIN</c>.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxRefinements"/> is negative.</exception>
    public TanhSinhIntegral(
        double relTolerance = 1.4901161193847656E-08,
        int maxRefinements = 15,
        double minComplement = NumericLimits.QL_MIN_POSITIVE_REAL * 4)
        : base(NumericLimits.QL_MAX_REAL, int.MaxValue)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxRefinements);
        this.relTolerance = relTolerance;
        tanhSinh = new TanhSinh(maxRefinements, minComplement);
    }

    /// <inheritdoc/>
    protected override IntegrationResult IntegrateCore(Func<double, double> f, double a, double b)
    {
        int evaluations = 0;
        double value = tanhSinh.Integrate(
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
