// SPDX-FileCopyrightText: Copyright (C) 2014 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/discreteintegrals.hpp ql/math/integrals/discreteintegrals.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// Simpson's rule on a fixed number of evenly spaced evaluations of a function over [a, b], the ends included, an odd
/// number of intervals closed by QuantLib's end correction.
/// </summary>
/// <remarks>
/// Immutable. Its accuracy is QuantLib's <c>Null&lt;Real&gt;()</c>: the rule runs its evaluations and reports no error.
/// Fewer than two evaluations are refused, where QuantLib's count wraps below zero (<c>discreteintegrals.cpp:92</c>);
/// at two, one interval, the trapezoid rule, where QuantLib's end correction counts f(a) a second time (2.52581 where
/// its <see cref="DiscreteSimpsonIntegral"/> on the same two points gives the trapezoid's 1.85914). From three
/// evaluations, QuantLib's arithmetic.
/// </remarks>
public sealed class DiscreteSimpsonIntegrator : Integrator
{
    private const double NullReal = 3.4028234663852886e38;

    /// <summary>Initializes a new instance of the <see cref="DiscreteSimpsonIntegrator"/> class.</summary>
    /// <param name="evaluations">The number of evaluations, at least 2.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="evaluations"/> is below 2.</exception>
    public DiscreteSimpsonIntegrator(int evaluations)
        : base(NullReal, DiscreteTrapezoidIntegrator.Evaluations(evaluations))
    {
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The evaluations were set below 2 (<see cref="Integrator.SetMaxEvaluations"/>).</exception>
    protected override IntegrationResult IntegrateCore(Func<double, double> f, double a, double b)
    {
        DiscreteTrapezoidIntegrator.CheckEvaluations(MaxEvaluations);
        int n = MaxEvaluations - 1;
        double d = (b - a) / n, d2 = d * 2;

        // one interval: the trapezoid
        if (n == 1)
        {
            return new IntegrationResult(0.5 * d * (f(a) + f(b)), MaxEvaluations);
        }

        double sum = 0.0, x = a + d;
        for (int i = 1; i < n; i += 2)
        {
            // to time 4
            sum += f(x);
            x += d2;
        }

        sum *= 2;

        x = a + d2;
        for (int i = 2; i < n - 1; i += 2)
        {
            // to time 2
            sum += f(x);
            x += d2;
        }

        sum *= 2;

        sum += f(a);
        if ((n & 1) != 0)
        {
            sum += (1.5 * f(b)) + (2.5 * f(b - d));
        }
        else
        {
            sum += f(b);
        }

        return new IntegrationResult(d / 3 * sum, MaxEvaluations);
    }
}
