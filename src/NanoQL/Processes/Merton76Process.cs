// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2004, 2005 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/processes/merton76process.hpp ql/processes/merton76process.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Processes;

/// <summary>
/// The Merton-76 jump-diffusion process: a Black-Scholes-Merton diffusion with lognormal jumps at a Poisson
/// intensity.
/// </summary>
/// <remarks>
/// Immutable: the jump intensity, the mean log jump and the log jump volatility are numbers, as every quote is (R1).
/// As QuantLib's, the process carries its parameters for the jump-diffusion engine and implements no dynamics: its
/// drift, diffusion and step refuse with QuantLib's texts, and so do its expectation, standard deviation and variance,
/// which the discretization reads from them.
/// </remarks>
public sealed class Merton76Process : StochasticProcess1D
{
    private readonly BlackScholesMertonProcess blackProcess;

    /// <summary>Initializes a new instance of the <see cref="Merton76Process"/> class.</summary>
    /// <param name="stateVariable">The underlying's value.</param>
    /// <param name="dividendTS">The dividend curve.</param>
    /// <param name="riskFreeTS">The risk-free curve.</param>
    /// <param name="blackVolTS">The Black volatility.</param>
    /// <param name="jumpIntensity">The jump intensity.</param>
    /// <param name="logMeanJump">The mean of the log jump.</param>
    /// <param name="logJumpVolatility">The standard deviation of the log jump.</param>
    /// <param name="d">The discretization, or <see langword="null"/> for QuantLib's default, Euler's.</param>
    /// <exception cref="ArgumentNullException">A curve or the volatility is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The Black volatility can't be read at time 0 and the spot.</exception>
    public Merton76Process(
        double stateVariable,
        YieldTermStructure dividendTS,
        YieldTermStructure riskFreeTS,
        BlackVolTermStructure blackVolTS,
        double jumpIntensity,
        double logMeanJump,
        double logJumpVolatility,
        StochasticProcess1DDiscretization? d = null)
        : base(d ?? new EulerDiscretization())
    {
        blackProcess = new BlackScholesMertonProcess(stateVariable, dividendTS, riskFreeTS, blackVolTS, discretization);
        JumpIntensity = jumpIntensity;
        LogMeanJump = logMeanJump;
        LogJumpVolatility = logJumpVolatility;
    }

    /// <inheritdoc/>
    public override double X0 => blackProcess.X0;

    /// <summary>Gets the underlying's value.</summary>
    public double StateVariable => blackProcess.StateVariable;

    /// <summary>Gets the dividend curve.</summary>
    public YieldTermStructure DividendYield => blackProcess.DividendYield;

    /// <summary>Gets the risk-free curve.</summary>
    public YieldTermStructure RiskFreeRate => blackProcess.RiskFreeRate;

    /// <summary>Gets the Black volatility.</summary>
    public BlackVolTermStructure BlackVolatility => blackProcess.BlackVolatility;

    /// <summary>Gets the jump intensity.</summary>
    public double JumpIntensity { get; }

    /// <summary>Gets the mean of the log jump.</summary>
    public double LogMeanJump { get; }

    /// <summary>Gets the standard deviation of the log jump.</summary>
    public double LogJumpVolatility { get; }

    /// <summary>The drift, which the process doesn't implement.</summary>
    /// <param name="t">The time.</param>
    /// <param name="x">The state.</param>
    /// <returns>Nothing: it throws.</returns>
    /// <exception cref="NanoQLException">Always, with QuantLib's text.</exception>
    public override double Drift(double t, double x) =>
        throw new NanoQLException("Merton76Process does not implement drift");

    /// <summary>The diffusion, which the process doesn't implement.</summary>
    /// <param name="t">The time.</param>
    /// <param name="x">The state.</param>
    /// <returns>Nothing: it throws.</returns>
    /// <exception cref="NanoQLException">Always, with QuantLib's text.</exception>
    public override double Diffusion(double t, double x) =>
        throw new NanoQLException("Merton76Process does not implement diffusion");

    /// <summary>The step, which the process doesn't implement.</summary>
    /// <param name="x0">The state.</param>
    /// <param name="dx">The increment.</param>
    /// <returns>Nothing: it throws.</returns>
    /// <exception cref="NanoQLException">Always, with QuantLib's text.</exception>
    public override double Apply(double x0, double dx) =>
        throw new NanoQLException("Merton76Process does not implement apply");

    /// <summary>The time of a date: the Black-Scholes-Merton process's, the risk-free curve's.</summary>
    /// <param name="date">The date.</param>
    /// <returns>The time.</returns>
    public override double Time(Date date) => blackProcess.Time(date);
}
