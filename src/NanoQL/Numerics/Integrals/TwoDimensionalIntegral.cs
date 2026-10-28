// SPDX-FileCopyrightText: Copyright (C) 2013 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/twodimensionalintegral.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// The integral of a two-dimensional function f(x, y) between (aₓ, a_y) and (bₓ, b_y), by two nested
/// one-dimensional integrations: the outer over x, the inner over y at each x.
/// </summary>
public sealed class TwoDimensionalIntegral
{
    private readonly Integrator integratorX;
    private readonly Integrator integratorY;

    /// <summary>Initializes a new instance of the <see cref="TwoDimensionalIntegral"/> class.</summary>
    /// <param name="integratorX">The outer integrator, over x.</param>
    /// <param name="integratorY">The inner integrator, over y.</param>
    /// <exception cref="ArgumentNullException">An integrator is null.</exception>
    public TwoDimensionalIntegral(Integrator integratorX, Integrator integratorY)
    {
        ArgumentNullException.ThrowIfNull(integratorX);
        ArgumentNullException.ThrowIfNull(integratorY);
        this.integratorX = integratorX;
        this.integratorY = integratorY;
    }

    /// <summary>
    /// The integral of <paramref name="f"/> over the rectangle from <paramref name="a"/> to <paramref name="b"/>.
    /// </summary>
    /// <param name="f">The integrand, of x and y.</param>
    /// <param name="a">The lower corner, (aₓ, a_y).</param>
    /// <param name="b">The upper corner, (bₓ, b_y).</param>
    /// <returns>The integral.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="f"/> is null.</exception>
    public double Integrate(Func<double, double, double> f, (double X, double Y) a, (double X, double Y) b)
    {
        ArgumentNullException.ThrowIfNull(f);
        return integratorX.Integrate(x => G(f, x, a.Y, b.Y), a.X, b.X);
    }

    private double G(Func<double, double, double> f, double x, double a, double b) =>
        integratorY.Integrate(y => f(x, y), a, b);
}
