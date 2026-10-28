// SPDX-FileCopyrightText: Copyright (C) 2014 Jose Aparicio
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/convolvedstudentt.hpp ql/experimental/math/convolvedstudentt.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// The inverse of <see cref="CumulativeBehrensFisher"/>, found by Brent's solver on the right half, the distribution
/// being symmetric.
/// </summary>
/// <remarks>
/// Immutable. The bracket runs from the normal's quantile times the factors' squared norm, a lower bound, to 1e6; a
/// probability so close to 1 that the cumulative stays below it there fails the solver's bracket check.
/// </remarks>
[Experimental("NQL0001")]
public sealed class InverseCumulativeBehrensFisher
{
    private readonly double normSqr;
    private readonly double accuracy;
    private readonly CumulativeBehrensFisher distrib;

    /// <summary>Initializes a new instance of the <see cref="InverseCumulativeBehrensFisher"/> class.</summary>
    /// <param name="degreesFreedom">The degrees of freedom of the t variables, odd.</param>
    /// <param name="factors">The factors of the linear combination.</param>
    /// <param name="accuracy">The root solver's accuracy.</param>
    /// <exception cref="ArgumentException">As <see cref="CumulativeBehrensFisher"/>'s constructor.</exception>
    public InverseCumulativeBehrensFisher(IReadOnlyList<int> degreesFreedom, IReadOnlyList<double> factors, double accuracy = 1.0e-6)
    {
        ArgumentNullException.ThrowIfNull(factors);
        double norm = 0.0;
        foreach (double f in factors)
        {
            norm += f * f;
        }

        normSqr = norm;
        this.accuracy = accuracy;
        distrib = new CumulativeBehrensFisher(degreesFreedom, factors);
    }

    /// <summary>The inverse cumulative value.</summary>
    /// <param name="q">The probability.</param>
    /// <returns>The value.</returns>
    /// <exception cref="NanoQLException">The solver fails, for a probability too close to 0 or 1.</exception>
    public double Value(double q)
    {
        double effectiveq;
        double sign;

        // since the distrib is symmetric solve only on the right side:
        if (q == 0.5)
        {
            return 0.0;
        }
        else if (q < 0.5)
        {
            sign = -1.0;
            effectiveq = 1.0 - q;
        }
        else
        {
            sign = 1.0;
            effectiveq = q;
        }

        double xMin = InverseCumulativeNormal.StandardValue(effectiveq) * normSqr;

        // inversion will fail at the Brent's bounds-check if this is not enough
        // (q is very close to 1.), in a bad combination fails around 1.-1.e-7
        double xMax = 1.0e6;
        return sign * new Brent().Solve(x => distrib.Value(x) - effectiveq, accuracy, (xMin + xMax) / 2.0, xMin, xMax);
    }
}
