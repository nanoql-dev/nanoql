// SPDX-FileCopyrightText: Copyright (C) 2017 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/experimental/volatility/sabrvoltermstructure.hpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Diagnostics.CodeAnalysis;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Experimental;

/// <summary>
/// A Black volatility term structure of SABR smiles on a forward growing at a constant rate: at time t the SABR
/// volatility (Hagan's 2002 expansion) at the forward s₀e^(rt).
/// </summary>
/// <remarks>Immutable; QuantLib's calendar is the null calendar and its convention Following.</remarks>
[Experimental("NQL0001")]
public sealed class SABRVolTermStructure : BlackVolatilityTermStructure
{
    private readonly double alpha;
    private readonly double beta;
    private readonly double gamma;
    private readonly double rho;
    private readonly double s0;
    private readonly double r;

    /// <summary>Initializes a new instance of the <see cref="SABRVolTermStructure"/> class.</summary>
    /// <param name="alpha">The SABR α.</param>
    /// <param name="beta">The SABR β.</param>
    /// <param name="gamma">The SABR volatility of volatility, ν.</param>
    /// <param name="rho">The SABR ρ.</param>
    /// <param name="s0">The underlying's value today.</param>
    /// <param name="r">The forward's continuous growth rate.</param>
    /// <param name="referenceDate">The reference date.</param>
    /// <param name="dayCounter">The day counter.</param>
    /// <exception cref="ArgumentNullException"><paramref name="dayCounter"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The reference date is <c>default(Date)</c>.</exception>
    public SABRVolTermStructure(
        double alpha, double beta, double gamma, double rho, double s0, double r, Date referenceDate, DayCounter dayCounter)
        : base(referenceDate, new NullCalendar(), BusinessDayConvention.Following, dayCounter)
    {
        this.alpha = alpha;
        this.beta = beta;
        this.gamma = gamma;
        this.rho = rho;
        this.s0 = s0;
        this.r = r;
    }

    /// <inheritdoc/>
    public override Date MaxDate => Date.MaxDate;

    /// <inheritdoc/>
    public override double MinStrike() => 0.0;

    /// <inheritdoc/>
    public override double MaxStrike() => NumericLimits.QL_MAX_REAL;

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">The strike, the forward, the time or the SABR parameters are out of
    /// <see cref="SabrFunctions.SabrVolatility"/>'s range.</exception>
    protected override double BlackVolImpl(double t, double strike)
    {
        double fwd = s0 * Math.Exp(r * t);
        return SabrFunctions.SabrVolatility(strike, fwd, t, alpha, beta, gamma, rho);
    }
}
