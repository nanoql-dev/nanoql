// SPDX-FileCopyrightText: Copyright (C) 2010 Adrian O' Neill
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/variancegamma/variancegammaprocess.hpp ql/experimental/variancegamma/variancegammaprocess.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.Processes;
using NanoQL.TermStructures;

namespace NanoQL.Experimental;

/// <summary>
/// The variance gamma process: a Brownian motion with drift θ and volatility σ, db = θ dt + σ dW, evaluated at the
/// times of a gamma process with mean 1 and variance rate ν.
/// </summary>
/// <remarks>
/// Immutable; it holds the spot, the curves and the three parameters, which its engines read. Its drift and diffusion
/// aren't implemented, as in QuantLib (no engine evolves it), and its default discretization is Euler's.
/// </remarks>
[Experimental("NQL0001")]
public sealed class VarianceGammaProcess : StochasticProcess1D
{
    /// <summary>Initializes a new instance of the <see cref="VarianceGammaProcess"/> class.</summary>
    /// <param name="s0">The spot.</param>
    /// <param name="dividendYield">The dividend yield curve.</param>
    /// <param name="riskFreeRate">The risk-free curve.</param>
    /// <param name="sigma">The Brownian motion's volatility σ.</param>
    /// <param name="nu">The gamma process's variance rate ν.</param>
    /// <param name="theta">The Brownian motion's drift θ.</param>
    /// <exception cref="ArgumentNullException">A curve is <see langword="null"/>.</exception>
    public VarianceGammaProcess(
        double s0, YieldTermStructure dividendYield, YieldTermStructure riskFreeRate, double sigma, double nu, double theta)
        : base(new EulerDiscretization())
    {
        ArgumentNullException.ThrowIfNull(dividendYield);
        ArgumentNullException.ThrowIfNull(riskFreeRate);
        S0 = s0;
        DividendYield = dividendYield;
        RiskFreeRate = riskFreeRate;
        Sigma = sigma;
        Nu = nu;
        Theta = theta;
    }

    /// <summary>Gets the spot.</summary>
    public double S0 { get; }

    /// <summary>Gets the dividend yield curve.</summary>
    public YieldTermStructure DividendYield { get; }

    /// <summary>Gets the risk-free curve.</summary>
    public YieldTermStructure RiskFreeRate { get; }

    /// <summary>Gets the Brownian motion's volatility σ.</summary>
    public double Sigma { get; }

    /// <summary>Gets the gamma process's variance rate ν.</summary>
    public double Nu { get; }

    /// <summary>Gets the Brownian motion's drift θ.</summary>
    public double Theta { get; }

    /// <inheritdoc/>
    public override double X0 => S0;

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">Always: "not implemented yet", as QuantLib's.</exception>
    public override double Drift(double t, double x) => throw new NanoQLException("not implemented yet");

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">Always: "not implemented yet", as QuantLib's.</exception>
    public override double Diffusion(double t, double x) => throw new NanoQLException("not implemented yet");
}
