// SPDX-FileCopyrightText: Copyright (C) 2003 Roman Gitlin
// SPDX-FileCopyrightText: Copyright (C) 2003 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/simpsonintegral.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The integral of a one-dimensional function by Simpson's formula: the trapezoid's refinements, each pair combined
/// as (4·I₂ₙ − Iₙ)/3, until two combined estimates differ by at most the accuracy, after at least six refinements.
/// </summary>
public sealed class SimpsonIntegral : TrapezoidIntegral<Default>
{
    /// <summary>Initializes a new instance of the <see cref="SimpsonIntegral"/> class.</summary>
    /// <param name="accuracy">The absolute accuracy, above machine epsilon.</param>
    /// <param name="maxIterations">The maximum number of refinements.</param>
    /// <exception cref="ArgumentException"><paramref name="accuracy"/> isn't above machine epsilon.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxIterations"/> is negative.</exception>
    public SimpsonIntegral(double accuracy, int maxIterations)
        : base(accuracy, maxIterations)
    {
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The accuracy isn't reached within the maximum number of
    /// refinements.</exception>
    protected override IntegrationResult IntegrateCore(Func<double, double> f, double a, double b)
    {
        // start from the coarsest trapezoid...
        int N = 1;
        double I = (f(a) + f(b)) * (b - a) / 2.0, newI;
        int evaluations = 2;

        double adjI = I, newAdjI;

        // ...and refine it
        int i = 1;
        do
        {
            newI = Default.Integrate(f, a, b, I, N);
            evaluations = checked(evaluations + N);
            N = checked(N * 2);
            newAdjI = ((4.0 * newI) - I) / 3.0;

            // good enough? Also, don't run away immediately
            if (Math.Abs(adjI - newAdjI) <= AbsoluteAccuracy && i > 5)
            {
                // ok, exit
                return new IntegrationResult(newAdjI, evaluations);
            }

            // oh well. Another step.
            I = newI;
            adjI = newAdjI;
            i++;
        }
        while (i < MaxEvaluations);

        throw new NanoQLException("max number of iterations reached");
    }
}
