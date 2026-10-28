// SPDX-FileCopyrightText: Copyright (C) 2008 Roland Lichters
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/distributions/studenttdistribution.hpp ql/math/distributions/studenttdistribution.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>
/// The Student t-distribution's inverse cumulative distribution function for n degrees of freedom. The implementation
/// delegates the calculation to Boost.Math's quantile, ported (<see cref="StudentsTFunctions"/>).
/// </summary>
/// <remarks>
/// Immutable. QuantLib's deprecated constructor taking a solver's accuracy and iterations, which it ignores, isn't
/// ported. At 4 and 6 degrees of freedom Boost takes Shaw's exact form and polynomial, which cancel near ½: within
/// 1e-9 of ½ they return ±0 for ±2.67e-9 (58% off at ½ − 1e-8 at n 4). There, where |p − ½| is below 0.05, nanoql
/// takes one Newton step on its cumulative function, within the quantile's conditioning, ε/(2|p − ½|) (a documented
/// divergence, found under R8; <c>Divergences/StudentDistributionTests.cs</c>); Boost's value, QuantLib's, bit for
/// bit, elsewhere.
/// </remarks>
public sealed class InverseCumulativeStudent
{
    private readonly int n;

    /// <summary>Initializes a new instance of the <see cref="InverseCumulativeStudent"/> class.</summary>
    /// <param name="n">The degrees of freedom, positive.</param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="n"/> isn't positive.</exception>
    public InverseCumulativeStudent(int n)
    {
        if (!(n > 0))
        {
            throw new ArgumentOutOfRangeException(nameof(n), n, "invalid parameter for t-distribution");
        }

        this.n = n;
    }

    /// <summary>The inverse cumulative distribution function.</summary>
    /// <param name="y">The probability, in (0, 1); a probability within 42 ulps of 1 gives the largest double, and one
    /// of magnitude below machine epsilon its negative.</param>
    /// <returns>The quantile.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="y"/> is outside (0, 1), and not one of
    /// those.</exception>
    public double Evaluate(double y)
    {
        if (y <= 0.0 || y >= 1.0)
        {
            if (ComparisonFunctions.CloseEnough(y, 1.0))
            {
                return NumericLimits.QL_MAX_REAL;
            }
            else if (Math.Abs(y) < NumericLimits.QL_EPSILON)
            {
                return NumericLimits.QL_MIN_REAL;
            }
            else
            {
                throw new ArgumentOutOfRangeException(
                    nameof(y),
                    y,
                    string.Create(CultureInfo.InvariantCulture, $"InverseCumulativeStudent({y}) undefined: must be 0 < x < 1"));
            }
        }

        if (y == 0.5)
        {
            return 0.0;
        }

        double q = StudentsTFunctions.Quantile(n, y);

        // Shaw's forms at n 4 and 6 cancel near the centre: one Newton step on the cumulative function there
        if ((n == 4 || n == 6) && Math.Abs(y - 0.5) < 0.05)
        {
            q -= (new CumulativeStudentDistribution(n).Evaluate(q) - y) / new StudentDistribution(n).Evaluate(q);
        }

        return q;
    }
}
