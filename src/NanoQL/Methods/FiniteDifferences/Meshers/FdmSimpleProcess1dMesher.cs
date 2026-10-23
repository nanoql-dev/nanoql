// SPDX-FileCopyrightText: Copyright (C) 2009, 2011 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/meshers/fdmsimpleprocess1dmesher.hpp ql/methods/finitedifferences/meshers/fdmsimpleprocess1dmesher.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.Methods;

/// <summary>
/// A one-dimensional mesher on a process's quantiles: each point the mean, over a number of times up to maturity, of
/// the process evolved from its initial value to that time with the standard normal quantile of evenly spaced
/// probabilities, the ends held to include the mandatory point and the initial value.
/// </summary>
/// <remarks>
/// A mesher of no point is refused, where QuantLib's reads the back of an empty vector, undefined behaviour (the S-sr
/// plan's SSR6 (xi), master §2.2's undefined-path row); one point is QuantLib's, the sum of the two ends' quantiles at
/// its one location.
/// </remarks>
public sealed class FdmSimpleProcess1dMesher : Fdm1dMesher
{
    /// <summary>Initializes a new instance of the <see cref="FdmSimpleProcess1dMesher"/> class.</summary>
    /// <param name="size">The number of points, at least 1.</param>
    /// <param name="process">The process.</param>
    /// <param name="maturity">The maturity.</param>
    /// <param name="tAvgSteps">The number of times the locations average over.</param>
    /// <param name="epsilon">The probability of the ends' quantiles.</param>
    /// <param name="mandatoryPoint">A point the ends hold; <see langword="null"/>, QuantLib's null, for the process's initial value.</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="size"/> or <paramref name="tAvgSteps"/> is negative.</exception>
    /// <exception cref="ArgumentException"><paramref name="size"/> is 0.</exception>
    public FdmSimpleProcess1dMesher(
        int size, StochasticProcess1D process, double maturity, int tAvgSteps = 10, double epsilon = 0.0001, double? mandatoryPoint = null)
        : base(size)
    {
        ArgumentNullException.ThrowIfNull(process);
        ArgumentOutOfRangeException.ThrowIfNegative(tAvgSteps);
        if (size == 0)
        {
            throw new ArgumentException("a mesher needs at least one point, not 0", nameof(size));
        }

        var inverse = new InverseCumulativeNormal();
        for (int l = 1; l <= tAvgSteps; ++l)
        {
            double t = maturity * l / tAvgSteps;

            double mp = mandatoryPoint ?? process.X0;

            double qMin = Min(mp, process.X0, process.Evolve(0, process.X0, t, inverse.Evaluate(epsilon)));
            double qMax = Max(mp, process.X0, process.Evolve(0, process.X0, t, inverse.Evaluate(1 - epsilon)));

            double dp = (1 - (2 * epsilon)) / (size - 1);
            double p = epsilon;
            locations[0] += qMin;

            for (int i = 1; i < size - 1; ++i)
            {
                p += dp;
                locations[i] += process.Evolve(0, process.X0, t, inverse.Evaluate(p));
            }

            locations[^1] += qMax;
        }

        for (int i = 0; i < size; ++i)
        {
            locations[i] /= tAvgSteps;
        }

        for (int i = 0; i < size - 1; ++i)
        {
            dminus[i + 1] = dplus[i] = locations[i + 1] - locations[i];
        }

        dplus[^1] = dminus[0] = NullReal;
    }

    // std::min({a, b, c}): the first of the smallest
    private static double Min(double a, double b, double c)
    {
        double m = a;
        if (b < m)
        {
            m = b;
        }

        if (c < m)
        {
            m = c;
        }

        return m;
    }

    // std::max({a, b, c}): the first of the largest
    private static double Max(double a, double b, double c)
    {
        double m = a;
        if (m < b)
        {
            m = b;
        }

        if (m < c)
        {
            m = c;
        }

        return m;
    }
}
