// SPDX-FileCopyrightText: Copyright (C) 2018 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/meshers/fdmcev1dmesher.hpp ql/methods/finitedifferences/meshers/fdmcev1dmesher.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>
/// A one-dimensional mesher for the constant elasticity of variance (CEV) process df = α·f^β·dW, absorbed at 0: from
/// the density's ε-quantile, divided by a scale factor, to its (1 − ε)-quantile, multiplied by it, concentrating around
/// a point where one is given within them, equally spaced otherwise. Where the density's mass at zero exceeds ε, the
/// lower end is 0, or <see cref="NumericLimits.QL_EPSILON"/> for β &lt; 0.
/// </summary>
/// <remarks>
/// Immutable. Within |β − 1| &lt; 1e-3 the bounds are the lognormal limit's quantiles, the CEV density at β = 1
/// (<see cref="CEVRNDCalculator"/>), where QuantLib's density throws or hangs in Boost's non-central chi-squared
/// (the S-van plan's VR10); elsewhere QuantLib's bounds and bits.
/// </remarks>
public sealed class FdmCEV1dMesher : Fdm1dMesher
{
    // the band around β = 1 the non-central chi-squared can't reach (VR10), CEVCalculator's
    private const double NearOne = 1e-3;

    /// <summary>Initializes a new instance of the <see cref="FdmCEV1dMesher"/> class.</summary>
    /// <param name="size">The number of points, at least 2.</param>
    /// <param name="f0">The forward's value today.</param>
    /// <param name="alpha">The volatility parameter α.</param>
    /// <param name="beta">The elasticity β.</param>
    /// <param name="maturity">The maturity.</param>
    /// <param name="eps">The probability outside each end.</param>
    /// <param name="scaleFactor">The factor the ends are widened by.</param>
    /// <param name="cPoint">
    /// The point to concentrate around and its density, relative to the mesher's length; <see langword="null"/> for
    /// none, QuantLib's <c>Null&lt;Real&gt;()</c>.
    /// </param>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> is negative.</exception>
    /// <exception cref="ArgumentException">
    /// The ends aren't in order, or <paramref name="size"/> is 0 or 1 (<see cref="Concentrating1dMesher"/>).
    /// </exception>
    /// <exception cref="NanoQLException">The density's quantile can't be computed.</exception>
    public FdmCEV1dMesher(
        int size,
        double f0,
        double alpha,
        double beta,
        double maturity,
        double eps = 0.0001,
        double scaleFactor = 1.5,
        (double? Point, double? Density) cPoint = default)
        : base(size)
    {
        // the lognormal limit in VR10's band
        var rndCalculator = new CEVRNDCalculator(f0, alpha, Math.Abs(beta - 1.0) < NearOne ? 1.0 : beta);
        double upperBound = scaleFactor * rndCalculator.InvCdf(1 - eps, maturity);

        double massAtZero = rndCalculator.MassAtZero(maturity);

        double lowerBound = (massAtZero > eps)
            ? ((beta < 0) ? NumericLimits.QL_EPSILON : 0.0)
            : rndCalculator.InvCdf(eps, maturity) / scaleFactor;

        Fdm1dMesher helper;
        if (cPoint.Point is double c && c >= lowerBound && c <= upperBound)
        {
            helper = new Concentrating1dMesher(lowerBound, upperBound, size, cPoint);
        }
        else
        {
            helper = new Uniform1dMesher(lowerBound, upperBound, size);
        }

        for (int i = 0; i < locations.Length; ++i)
        {
            locations[i] = helper.Location(i);
            dplus[i] = helper.Dplus(i);
            dminus[i] = helper.Dminus(i);
        }
    }
}
