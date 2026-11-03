// SPDX-FileCopyrightText: Copyright (C) 2009 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/meshers/fdmblackscholesmultistrikemesher.hpp ql/methods/finitedifferences/meshers/fdmblackscholesmultistrikemesher.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.Processes;
using NanoQL.TermStructures;

namespace NanoQL.Methods;

/// <summary>
/// The one-dimensional mesher of a Black-Scholes process in the logarithm of the underlying for several strikes at
/// once: from below the lowest to above the highest of the forwards S²/K·d the strikes give, by a multiple of the
/// standard deviation at each, concentrating around a critical point when one is given inside.
/// </summary>
/// <remarks>
/// Immutable. QuantLib reads the curves and the volatility at the risk-free curve's times
/// (<c>fdmblackscholesmultistrikemesher.cpp:45-62</c>); here each at the instants its maturity denotes on
/// <see cref="FdmBlackScholesOp.Clock"/>, on its own clock, as <see cref="FdmBlackScholesMesher"/> reads them, and
/// QuantLib's reads where they share the risk-free curve's day counter and reference date.
/// </remarks>
public sealed class FdmBlackScholesMultiStrikeMesher : Fdm1dMesher
{
    /// <summary>Initializes a new instance of the <see cref="FdmBlackScholesMultiStrikeMesher"/> class.</summary>
    /// <param name="size">The number of points, at least 2.</param>
    /// <param name="process">The Black-Scholes process.</param>
    /// <param name="maturity">The maturity, a time on <see cref="FdmBlackScholesOp.Clock"/>.</param>
    /// <param name="strikes">The strikes, at least one.</param>
    /// <param name="eps">The probability left outside each edge.</param>
    /// <param name="scaleFactor">The factor widening the edges.</param>
    /// <param name="cPoint">
    /// The critical point, in the underlying's units, and its density; <see langword="null"/> for none, QuantLib's
    /// <c>Null&lt;Real&gt;()</c>.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> or <paramref name="strikes"/> is
    /// <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">
    /// The spot isn't positive, no strike is given, the lowest forward isn't positive, or the size is below 2.
    /// </exception>
    /// <exception cref="NanoQLException">A curve or the volatility can't be read.</exception>
    public FdmBlackScholesMultiStrikeMesher(
        int size,
        GeneralizedBlackScholesProcess process,
        double maturity,
        IEnumerable<double> strikes,
        double eps = 0.0001,
        double scaleFactor = 1.5,
        (double? Point, double? Density) cPoint = default)
        : this(size, process, maturity, strikes, eps, scaleFactor, cPoint, FdmBlackScholesOp.Clock(process))
    {
    }

    // The finite-difference Heston engines' form: the engine's clock given, its own and not the helper process's, whose
    // curves QuantLib's engines bind in each other's places.
    internal FdmBlackScholesMultiStrikeMesher(
        int size,
        GeneralizedBlackScholesProcess process,
        double maturity,
        IEnumerable<double> strikes,
        double eps,
        double scaleFactor,
        (double? Point, double? Density) cPoint,
        TermStructure clock)
        : base(size)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentNullException.ThrowIfNull(strikes);
        (double xMin, double xMax) = Bounds(process, maturity, [.. strikes], eps, scaleFactor, clock);

        Fdm1dMesher helper;
        if (cPoint.Point is double c
            && Math.Log(c) >= xMin && Math.Log(c) <= xMax)
        {
            helper = new Concentrating1dMesher(xMin, xMax, size, (Math.Log(c), cPoint.Density));
        }
        else
        {
            helper = new Uniform1dMesher(xMin, xMax, size);
        }

        for (int i = 0; i < size; ++i)
        {
            locations[i] = helper.Location(i);
            dplus[i] = helper.Dplus(i);
            dminus[i] = helper.Dminus(i);
        }
    }

    // The grid's edges: below the lowest and above the highest strike's mirrored forward, spot^2/K times the forward
    // factor, by the volatility's quantile at 1 - eps times the scale factor, or 0.8 and 1.2 times the mirrored spot's
    // logarithm where that reaches further
    internal static (double XMin, double XMax) Bounds(
        GeneralizedBlackScholesProcess process, double maturity, double[] k, double eps, double scaleFactor, TermStructure clock)
    {
        double spot = process.X0;
        if (!(spot > 0.0))
        {
            throw new ArgumentException("negative or null underlying given", "process");
        }

        // std::min_element over an empty range is undefined
        if (k.Length == 0)
        {
            throw new ArgumentException("no strikes given", "strikes");
        }

        double d = process.DividendYield.DiscountAt(clock, maturity) / process.RiskFreeRate.DiscountAt(clock, maturity);

        // std::min_element and std::max_element: the first extremum by operator<
        double minStrike = k[0], maxStrike = k[0];
        for (int i = 1; i < k.Length; ++i)
        {
            if (k[i] < minStrike)
            {
                minStrike = k[i];
            }

            if (maxStrike < k[i])
            {
                maxStrike = k[i];
            }
        }

        double fmin = spot * spot / maxStrike * d;
        double fmax = spot * spot / minStrike * d;

        if (!(fmin > 0.0))
        {
            throw new ArgumentException("negative forward given", "strikes");
        }

        // Set the grid boundaries
        double normInvEps = new InverseCumulativeNormal().Evaluate(1 - eps);
        double sigmaSqrtTmin = SigmaSqrtT(process.BlackVolatility, clock, maturity, minStrike);
        double sigmaSqrtTmax = SigmaSqrtT(process.BlackVolatility, clock, maturity, maxStrike);

        double xMin = Min(
            0.8 * Math.Log(0.8 * spot * spot / maxStrike),
            Math.Log(fmin) - (sigmaSqrtTmin * normInvEps * scaleFactor) - (sigmaSqrtTmin * sigmaSqrtTmin / 2.0));
        double xMax = Max(
            1.2 * Math.Log(0.8 * spot * spot / minStrike),
            Math.Log(fmax) + (sigmaSqrtTmax * normInvEps * scaleFactor) - (sigmaSqrtTmax * sigmaSqrtTmax / 2.0));
        return (xMin, xMax);
    }

    // blackVol(T, K)·√T, the variance accrued to the maturity's instant where the volatility has its own clock
    private static double SigmaSqrtT(BlackVolTermStructure volTS, TermStructure clock, double maturity, double strike) =>
        volTS.SharesTimes(clock)
            ? volTS.BlackVol(maturity, strike) * Math.Sqrt(maturity)
            : Math.Sqrt(volTS.BlackForwardVarianceAt(clock, 0.0, maturity, strike));

    private static double Min(double a, double b) => b < a ? b : a;

    private static double Max(double a, double b) => a < b ? b : a;
}
