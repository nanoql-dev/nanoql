// SPDX-FileCopyrightText: Copyright (C) 2016 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/randomnumbers/stochasticcollocationinvcdf.hpp ql/math/randomnumbers/stochasticcollocationinvcdf.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The stochastic collocation inverse cumulative distribution function: an inverse cumulative function approximated by
/// a Lagrange polynomial in a standard normal deviate, collocated at the Gauss-Hermite nodes.
/// </summary>
/// <remarks>
/// <para>
/// References: L.A. Grzelak, J.A.S. Witteveen, M.Suárez-Taboada, C.W. Oosterlee, The Stochastic Collocation Monte
/// Carlo Sampler: Highly efficient sampling from “expensive” distributions,
/// http://papers.ssrn.com/sol3/papers.cfm?abstract_id=2529691.
/// </para>
/// <para>
/// Immutable: the nodes, their scale and the collocated values are computed once, at construction, where the inverse
/// cumulative function given is called once per node.
/// </para>
/// </remarks>
public sealed class StochasticCollocationInvCDF
{
    private readonly double sigma;
    private readonly LagrangeInterpolation interpl;

    /// <summary>Initializes a new instance of the <see cref="StochasticCollocationInvCDF"/> class.</summary>
    /// <param name="invCDF">The inverse cumulative distribution function to approximate.</param>
    /// <param name="lagrangeOrder">The number of collocation points, at least 1.</param>
    /// <param name="pMax">
    /// The probability the largest node collocates, or null; the nodes are scaled so that it does.
    /// </param>
    /// <param name="pMin">
    /// The probability the smallest node collocates where <paramref name="pMax"/> is null, or null; with both null the
    /// nodes are the standard normal's.
    /// </param>
    /// <exception cref="ArgumentNullException"><paramref name="invCDF"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="lagrangeOrder"/> is below 1.</exception>
    public StochasticCollocationInvCDF(
        Func<double, double> invCDF, int lagrangeOrder, double? pMax = null, double? pMin = null)
    {
        ArgumentNullException.ThrowIfNull(invCDF);
        Vector hermite = new GaussHermiteIntegration(lagrangeOrder).X();
        var x = new double[hermite.Size];
        for (int i = 0; i < x.Length; i++)
        {
            x[i] = MathConstants.M_SQRT2 * hermite[i];
        }

        sigma = pMax is double max
            ? x[^1] / new InverseCumulativeNormal().Evaluate(max)
            : pMin is double min
                ? x[0] / new InverseCumulativeNormal().Evaluate(min)
                : 1.0;
        var y = new double[x.Length];
        var normalCDF = new CumulativeNormalDistribution();
        for (int i = 0, n = x.Length; i < n; ++i)
        {
            y[i] = invCDF(normalCDF.Evaluate(x[i] / sigma));
        }

        interpl = new LagrangeInterpolation(x, y);
    }

    /// <summary>The approximation at a standard normal deviate.</summary>
    /// <param name="x">The deviate.</param>
    /// <returns>The approximated inverse cumulative function at the deviate's probability.</returns>
    public double Value(double x) => interpl.Evaluate(x * sigma, true);

    /// <summary>The approximation at a probability.</summary>
    /// <param name="u">The probability, in (0, 1).</param>
    /// <returns>The approximated inverse cumulative function at <paramref name="u"/>.</returns>
    public double Evaluate(double u) => Value(new InverseCumulativeNormal().Evaluate(u));
}
