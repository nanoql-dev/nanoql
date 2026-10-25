// SPDX-FileCopyrightText: Copyright (C) 2014 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/discreteintegrals.hpp ql/math/integrals/discreteintegrals.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The trapezoid rule on a fixed number of evenly spaced evaluations of a function over [a, b], the ends included.
/// </summary>
/// <remarks>
/// Immutable. Its accuracy is QuantLib's <c>Null&lt;Real&gt;()</c>: the rule runs its evaluations and reports no error.
/// Fewer than two evaluations are refused, where QuantLib's count of intervals wraps below zero and its loop runs about
/// 2⁶⁴ times (<c>discreteintegrals.cpp:74-79</c>; the S-van plan's VR4 (xix)).
/// </remarks>
public sealed class DiscreteTrapezoidIntegrator : Integrator
{
    private const double NullReal = 3.4028234663852886e38;

    /// <summary>Initializes a new instance of the <see cref="DiscreteTrapezoidIntegrator"/> class.</summary>
    /// <param name="evaluations">The number of evaluations, at least 2.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="evaluations"/> is below 2.</exception>
    public DiscreteTrapezoidIntegrator(int evaluations)
        : base(NullReal, Evaluations(evaluations))
    {
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The evaluations were set below 2 (<see cref="Integrator.SetMaxEvaluations"/>).</exception>
    protected override IntegrationResult IntegrateCore(Func<double, double> f, double a, double b)
    {
        CheckEvaluations(MaxEvaluations);
        int n = MaxEvaluations - 1;
        double d = (b - a) / n;

        double sum = f(a) * 0.5;

        for (int i = 0; i < n - 1; ++i)
        {
            a += d;
            sum += f(a);
        }

        sum += f(b) * 0.5;

        return new IntegrationResult(d * sum, MaxEvaluations);
    }

    // at least two, where QuantLib's Size wraps below zero (VR4 (xix))
    internal static int Evaluations(int evaluations)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(evaluations, 2);
        return evaluations;
    }

    // the same at integration, the evaluations set since by SetMaxEvaluations
    internal static void CheckEvaluations(int evaluations)
    {
        if (evaluations < 2)
        {
            throw new NanoQLException(
                string.Create(
                    System.Globalization.CultureInfo.InvariantCulture,
                    $"at least two evaluations are needed, {evaluations} given"));
        }
    }
}
