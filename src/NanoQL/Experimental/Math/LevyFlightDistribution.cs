// SPDX-FileCopyrightText: Copyright (C) 2015 Andres Hernandez
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/math/levyflightdistribution.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// Partial: LevyFlightDistribution::param_type, its constructor from one, param(), reset() and operator()(Engine&, const param_type&), never ported: the standard library's distribution concept, which no QuantLib code reads

using System.Diagnostics.CodeAnalysis;
using NanoQL.Numerics;

namespace NanoQL.Experimental;

/// <summary>
/// The Levy flight distribution, p(x) = α x_m^α / x^(α+1) on [x_m, ∞) with α &gt; 0: QuantLib's
/// <c>LevyFlightDistribution</c>.
/// </summary>
/// <remarks>
/// The Levy flight is normally defined with x_m = 1 and 0 &lt; α &lt; 2, where p(x) has an infinite variance. The more
/// general Pareto Type I distribution is well defined for α &gt; 2, so α isn't restricted to be below 2. Immutable.
/// </remarks>
[Experimental("NQL0001")]
public sealed class LevyFlightDistribution : IRandomWalkDistribution
{
    /// <summary>Initializes a new instance of the <see cref="LevyFlightDistribution"/> class.</summary>
    /// <param name="xm">The lower end x_m.</param>
    /// <param name="alpha">The exponent α, positive.</param>
    /// <exception cref="ArgumentException"><paramref name="alpha"/> isn't positive, with QuantLib's text.</exception>
    public LevyFlightDistribution(double xm = 1.0, double alpha = 1.0)
    {
        if (!(alpha > 0.0))
        {
            throw new ArgumentException("alpha must be larger than 0", nameof(alpha));
        }

        Xm = xm;
        Alpha = alpha;
    }

    /// <summary>Gets the lower end x_m.</summary>
    public double Xm { get; }

    /// <summary>Gets the exponent α.</summary>
    public double Alpha { get; }

    /// <summary>Gets the lowest value, x_m: QuantLib's <c>min()</c>.</summary>
    public double Min => Xm;

    /// <summary>Gets the highest value, the largest double: QuantLib's <c>max()</c>.</summary>
    public double Max => NumericLimits.QL_MAX_REAL;

    /// <summary>The density at x: QuantLib's <c>operator()(Real x)</c>.</summary>
    /// <param name="x">The point.</param>
    /// <returns>α (x_m/x)^α / x, and 0 below x_m.</returns>
    public double Density(double x)
    {
        if (x < Xm)
        {
            return 0.0;
        }

        return Alpha * Math.Pow(Xm / x, Alpha) / x;
    }

    /// <summary>
    /// A draw, x_m · u^(−1/α) with u from <c>std::uniform_real_distribution&lt;Real&gt;(0.0, 1.0)</c> as MSVC draws it:
    /// QuantLib's <c>operator()(Engine&amp;)</c>.
    /// </summary>
    /// <param name="engine">The engine.</param>
    /// <returns>The draw.</returns>
    public double Next(MersenneTwisterUniformRng engine)
    {
        ArgumentNullException.ThrowIfNull(engine);
        return Xm * Math.Pow(MsvcRandom.UniformReal(engine, 0.0, 1.0), -1.0 / Alpha);
    }

    /// <inheritdoc/>
    /// <remarks>Immutable: the distribution itself.</remarks>
    public IRandomWalkDistribution Clone() => this;
}
