// SPDX-FileCopyrightText: Copyright (C) 2006 Warren Chou
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/forward/mcvarianceswapengine.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Processes;

namespace NanoQL.PricingEngines;

/// <summary>
/// The realized variance of a path: the process's squared diffusion at each grid point, integrated over the path by
/// the segment rule on the grid's intervals, per unit of time.
/// </summary>
/// <remarks>
/// Immutable. It integrates over the path it is given, where QuantLib's integrand copies it per sample
/// (<c>mcvarianceswapengine.hpp:313-318</c>), so a sample allocates nothing; the segment rule's arithmetic is
/// QuantLib's.
/// </remarks>
public sealed class VariancePathPricer : PathPricer<MonteCarloPath>
{
    private readonly StochasticProcess1D process;

    /// <summary>Initializes a new instance of the <see cref="VariancePathPricer"/> class.</summary>
    /// <param name="process">The Black-Scholes process whose diffusion the path's variance reads.</param>
    /// <exception cref="ArgumentNullException"><paramref name="process"/> is <see langword="null"/>.</exception>
    public VariancePathPricer(GeneralizedBlackScholesProcess process)
        : this((StochasticProcess1D)process)
    {
    }

    // The engine's: the process read on its clock.
    internal VariancePathPricer(StochasticProcess1D process)
    {
        ArgumentNullException.ThrowIfNull(process);
        this.process = process;
    }

    /// <summary>The path's realized variance per unit of time.</summary>
    /// <param name="path">The path.</param>
    /// <returns>The variance.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The path is empty.</exception>
    /// <exception cref="ArgumentException">
    /// The path has no step, whose first step QuantLib reads past a one-point grid; or the grid's span holds no whole
    /// first step, which no grid starting at 0 reaches.
    /// </exception>
    public override double Evaluate(MonteCarloPath path)
    {
        ArgumentNullException.ThrowIfNull(path);
        if (path.Empty)
        {
            throw new NanoQLException("the path cannot be empty");
        }

        // a one-point path has no first step, which QuantLib reads past its grid's end (undefined behaviour)
        if (path.Length < 2)
        {
            throw new ArgumentException("the path has no step", nameof(path));
        }

        NanoQL.TimeGrid grid = path.TimeGrid;
        double t0 = grid.Front();
        double t = grid.Back();
        double dt = grid.Dt(0);

        // QuantLib's SegmentIntegral(static_cast<Size>(t/dt)) over the integrand, inline
        int intervals = (int)(t / dt);
        if (!(intervals > 0))
        {
            throw new ArgumentException($"at least 1 interval needed, {intervals} given", nameof(path));
        }

        double integral;
        if (t0 == t || ComparisonFunctions.CloseEnough(t0, t))
        {
            integral = 0.0;
        }
        else
        {
            double h = (t - t0) / intervals;
            double sum = 0.5 * (Integrand(path, dt, t0) + Integrand(path, dt, t));
            double end = t - (0.5 * h);
            for (double x = t0 + h; x < end; x += h)
            {
                sum += Integrand(path, dt, x);
            }

            integral = sum * h;
        }

        return integral / t;
    }

    // QuantLib's detail::Integrand: the squared diffusion at the grid point below the time.
    private double Integrand(MonteCarloPath path, double dt, double time)
    {
        int i = (int)(time / dt);
        double sigma = process.Diffusion(time, path[i]);
        return sigma * sigma;
    }
}
