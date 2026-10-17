// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2004 Walter Penschke
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/distributions/poissondistribution.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The inverse cumulative Poisson distribution: the smallest integer whose cumulative probability reaches a given
/// probability, for a mean λ.
/// </summary>
/// <remarks>
/// A configuration struct: its zero value (<c>default</c>, <c>new InverseCumulativePoisson()</c>, an unassigned field
/// or array element) is QuantLib's default construction, λ = 1.
/// </remarks>
public readonly struct InverseCumulativePoisson : IInverseCumulative
{
    // QuantLib's lambda. The constructor refuses 0, so a stored 0 is the zero value, which reads as QuantLib's
    // default, 1.
    private readonly double lambda;

    /// <summary>Initializes a new instance of the <see cref="InverseCumulativePoisson"/> struct.</summary>
    /// <param name="lambda">The mean, positive.</param>
    /// <exception cref="ArgumentException"><paramref name="lambda"/> isn't positive.</exception>
    public InverseCumulativePoisson(double lambda = 1.0)
    {
        if (!(lambda > 0.0))
        {
            throw new ArgumentException("lambda must be positive", nameof(lambda));
        }

        this.lambda = lambda;
    }

    private double Lambda => lambda == 0.0 ? 1.0 : lambda;

    /// <summary>The inverse cumulative distribution at <paramref name="x"/>.</summary>
    /// <param name="x">The probability, in [0, 1].</param>
    /// <returns>The integer, as a real; 0 at 0, and the largest double at 1.</returns>
    /// <exception cref="ArgumentException"><paramref name="x"/> is outside [0, 1], or NaN.</exception>
    public double Evaluate(double x)
    {
        if (!(x >= 0.0 && x <= 1.0))
        {
            throw new ArgumentException(
                "Inverse cumulative Poisson distribution is only defined on the interval [0,1]", nameof(x));
        }

        if (x == 1.0)
        {
            return NumericLimits.QL_MAX_REAL;
        }

        if (x == 0.0)
        {
            return 0.0;
        }

        double sum = 0.0;
        uint index = 0;
        while (x > sum)
        {
            sum += CalcSummand(index);
            index++;
        }

        return unchecked(index - 1);
    }

    // QuantLib's BigNatural index; std::pow(lambda_, Integer(index)) and Factorial::get(index) read it as 32-bit
    // values.
    private double CalcSummand(uint index) =>
        Math.Exp(-Lambda) * Math.Pow(Lambda, unchecked((int)index)) / Factorial.Get(index);
}
