// SPDX-FileCopyrightText: Copyright (C) 2003 Roman Gitlin
// SPDX-FileCopyrightText: Copyright (C) 2003 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/trapezoidintegral.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The integral of a one-dimensional function by the trapezoid formula, its number of intervals refined until two
/// estimates differ by at most the accuracy.
/// </summary>
/// <remarks>
/// <para>
/// Given a target accuracy ε, the integral of f between a and b is calculated by means of the trapezoid formula
/// ∫ f dx = ½f(x₀) + f(x₁) + … + f(x_{N−1}) + ½f(x_N), where x₀ = a, x_N = b and xᵢ = a + iΔx with Δx = (b − a)/N.
/// The number N of intervals is repeatedly increased until the target accuracy is reached, after at least six
/// refinements; the maximum number of evaluations bounds the refinements, not the evaluations.
/// </para>
/// <para>
/// The refinements start from the policy's own one-interval estimate: the trapezoid's for <see cref="Default"/>, as in
/// QuantLib, and the midpoint's for <see cref="MidPoint"/>, where QuantLib starts from the trapezoid's (see
/// <see cref="MidPoint"/>).
/// </para>
/// </remarks>
/// <typeparam name="TIntegrationPolicy">The refinement: <see cref="Default"/> or <see cref="MidPoint"/>.</typeparam>
public class TrapezoidIntegral<TIntegrationPolicy> : Integrator
    where TIntegrationPolicy : struct, ITrapezoidIntegrationPolicy
{
    /// <summary>Initializes a new instance of the <see cref="TrapezoidIntegral{TIntegrationPolicy}"/> class.</summary>
    /// <param name="accuracy">The absolute accuracy, above machine epsilon.</param>
    /// <param name="maxIterations">The maximum number of refinements.</param>
    /// <exception cref="ArgumentException"><paramref name="accuracy"/> isn't above machine epsilon.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxIterations"/> is negative.</exception>
    public TrapezoidIntegral(double accuracy, int maxIterations)
        : base(accuracy, maxIterations)
    {
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The accuracy isn't reached within the maximum number of
    /// refinements.</exception>
    protected override IntegrationResult IntegrateCore(Func<double, double> f, double a, double b)
    {
        // start from the coarsest estimate...
        int N = 1;
        IntegrationResult start = TIntegrationPolicy.InitialEstimate(f, a, b);
        double I = start.Value, newI;
        int evaluations = start.NumberOfEvaluations;

        // ...and refine it
        int i = 1;
        do
        {
            newI = TIntegrationPolicy.Integrate(f, a, b, I, N);
            evaluations = checked(evaluations + (N * (TIntegrationPolicy.NbEvalutions() - 1)));
            N = checked(N * TIntegrationPolicy.NbEvalutions());

            // good enough? Also, don't run away immediately
            if (Math.Abs(I - newI) <= AbsoluteAccuracy && i > 5)
            {
                // ok, exit
                return new IntegrationResult(newI, evaluations);
            }

            // oh well. Another step.
            I = newI;
            i++;
        }
        while (i < MaxEvaluations);

        throw new NanoQLException("max number of iterations reached");
    }
}
