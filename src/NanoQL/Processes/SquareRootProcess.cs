// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2004, 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/processes/squarerootprocess.hpp ql/processes/squarerootprocess.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

namespace NanoQL.Processes;

/// <summary>The square-root process, governed by dx = a(b − x_t) dt + σ√x_t dW_t.</summary>
/// <remarks>
/// The diffusion at a negative state is 0, the root of the state floored at 0, as <see cref="HestonProcess"/>'s
/// partial truncation takes its variance's: a divergence, QuantLib's σ√x being NaN there, so that an Euler path, which
/// can step below 0, turns NaN for good (<c>Divergences/SquareRootProcessTruncationTests.cs</c>). The drift reads the
/// state as it is, as the partial truncation's does.
/// </remarks>
public sealed class SquareRootProcess : StochasticProcess1D
{
    private readonly double x0;
    private readonly double mean;
    private readonly double speed;
    private readonly double volatility;

    /// <summary>Initializes a new instance of the <see cref="SquareRootProcess"/> class.</summary>
    /// <param name="b">The mean level, b.</param>
    /// <param name="a">The speed of mean reversion, a.</param>
    /// <param name="sigma">The volatility, σ.</param>
    /// <param name="x0">The initial value.</param>
    /// <param name="d">The discretization; <see langword="null"/> for Euler's, QuantLib's default.</param>
    public SquareRootProcess(
        double b, double a, double sigma, double x0 = 0.0, StochasticProcess1DDiscretization? d = null)
        : base(d ?? new EulerDiscretization())
    {
        this.x0 = x0;
        mean = b;
        speed = a;
        volatility = sigma;
    }

    /// <inheritdoc/>
    public override double X0 => x0;

    /// <summary>Gets the speed of mean reversion, a.</summary>
    public double A => speed;

    /// <summary>Gets the mean level, b.</summary>
    public double B => mean;

    /// <summary>Gets the volatility, σ.</summary>
    public double Sigma => volatility;

    /// <inheritdoc/>
    public override double Drift(double t, double x) => speed * (mean - x);

    /// <inheritdoc/>
    public override double Diffusion(double t, double x) => volatility * (x > 0.0 ? Math.Sqrt(x) : 0.0);
}
