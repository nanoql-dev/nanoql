// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/hybridsimulatedannealingfunctors.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// A reannealing by finite differences, which raises the temperature the more sensitive dimensions see so the search
/// concentrates on them: QuantLib's <c>ReannealingFiniteDifferences</c>.
/// </summary>
/// <remarks>
/// In a multidimensional problem the dimensions can differ in sensitivity, and the solution can be insensitive to
/// some; each dimension's sensitivity is a forward difference of the problem's value, times its bounds' width where
/// bounds are given, and each step becomes |ln(s_max/s · T₀/T)|^n. Each minimization's copy evaluates that
/// minimization's problem, counting its evaluations.
/// </remarks>
[Experimental("NQL0001")]
public sealed class ReannealingFiniteDifferences : IAnnealingReannealing<ReannealingFiniteDifferences>
{
    private readonly double stepSize, minSize, functionTol;
    private readonly int N;
    private readonly Vector initialTemp, bounded;
    private ProblemState? problem;

    /// <summary>Initializes a new instance of the <see cref="ReannealingFiniteDifferences"/> class.</summary>
    /// <param name="initialTemp">The initial temperature T₀.</param>
    /// <param name="dimension">The dimension n.</param>
    /// <param name="lower">The lower bounds; <see langword="null"/> or empty for none.</param>
    /// <param name="upper">The upper bounds; <see langword="null"/> or empty for none.</param>
    /// <param name="stepSize">The finite difference's step.</param>
    /// <param name="minSize">The smallest sensitivity.</param>
    /// <param name="functionTol">The smallest ratio of sensitivities and temperatures.</param>
    /// <exception cref="ArgumentException">The bounds are not one per dimension, with QuantLib's text.</exception>
    public ReannealingFiniteDifferences(
        double initialTemp,
        int dimension,
        Vector? lower = null,
        Vector? upper = null,
        double stepSize = 1e-7,
        double minSize = 1e-10,
        double functionTol = 1e-10)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(dimension);
        this.stepSize = stepSize;
        this.minSize = minSize;
        this.functionTol = functionTol;
        N = dimension;
        this.initialTemp = new Vector(dimension, initialTemp);
        bounded = new Vector(dimension, 1.0);
        if (lower is not null && !lower.Empty && upper is not null && !upper.Empty)
        {
            if (lower.Size != N)
            {
                throw new ArgumentException("Incompatible input", nameof(lower));
            }

            if (upper.Size != N)
            {
                throw new ArgumentException("Incompatible input", nameof(upper));
            }

            for (int i = 0; i < N; i++)
            {
                bounded[i] = upper[i] - lower[i];
            }
        }
    }

    private ReannealingFiniteDifferences(ReannealingFiniteDifferences other)
    {
        stepSize = other.stepSize;
        minSize = other.minSize;
        functionTol = other.functionTol;
        N = other.N;
        initialTemp = other.initialTemp;
        bounded = other.bounded;
    }

    /// <inheritdoc/>
    public void SetProblem(ProblemState P) => problem = P;

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The vectors' sizes are not the dimension, with QuantLib's text.</exception>
    /// <exception cref="NanoQLException">No problem is set.</exception>
    public void Reanneal(Vector steps, Vector currentPoint, double aCurrentValue, Vector currTemp)
    {
        ArgumentNullException.ThrowIfNull(steps);
        ArgumentNullException.ThrowIfNull(currentPoint);
        ArgumentNullException.ThrowIfNull(currTemp);
        if (currTemp.Size != N || steps.Size != N)
        {
            throw new ArgumentException("Incompatible input");
        }

        ProblemState p = problem ?? throw new NanoQLException("the reannealing has no problem set");
        var finiteDiffs = new Vector(N, 0.0);
        double finiteDiffMax = 0.0;
        Vector ofssetPoint = currentPoint.Clone();
        for (int i = 0; i < N; i++)
        {
            ofssetPoint[i] += stepSize;
            finiteDiffs[i] = bounded[i] * Math.Abs((p.Value(ofssetPoint) - aCurrentValue) / stepSize);
            ofssetPoint[i] -= stepSize;
            if (finiteDiffs[i] < minSize)
            {
                finiteDiffs[i] = minSize;
            }

            if (finiteDiffs[i] > finiteDiffMax)
            {
                finiteDiffMax = finiteDiffs[i];
            }
        }

        for (int i = 0; i < N; i++)
        {
            double tRatio = initialTemp[i] / currTemp[i];
            double sRatio = finiteDiffMax / finiteDiffs[i];
            if (sRatio * tRatio < functionTol)
            {
                steps[i] = Math.Pow(Math.Abs(Math.Log(functionTol)), N);
            }
            else
            {
                steps[i] = Math.Pow(Math.Abs(Math.Log(sRatio * tRatio)), N);
            }
        }
    }

    /// <inheritdoc/>
    public ReannealingFiniteDifferences Clone() => new(this);
}
