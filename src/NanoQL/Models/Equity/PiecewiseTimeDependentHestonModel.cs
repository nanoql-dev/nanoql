// SPDX-FileCopyrightText: Copyright (C) 2010 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/models/equity/piecewisetimedependenthestonmodel.hpp ql/models/equity/piecewisetimedependenthestonmodel.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Models;

/// <summary>The Heston model with piecewise time-dependent parameters on a time grid, calibrated.</summary>
/// <remarks>
/// <para>
/// Its arguments are θ, κ, σ, ρ and v₀, in that order: the first four as given, v₀ a positive constant parameter.
/// Immutable: <see cref="CalibratedModel.WithParams"/> returns a new model. The model's time runs from the earlier of
/// its curves' reference dates on the risk-free curve's day counter, and its parameters' grid is on that clock, where
/// QuantLib's runs from the risk-free curve's reference date and reads the dividend curve at the risk-free curve's
/// times; a market whose dividend curve isn't dated before the risk-free curve keeps QuantLib's times.
/// </para>
/// <para>
/// References: S. Heston, A Closed-Form Solution for Options with Stochastic Volatility with Applications to Bond and
/// Currency Options, The Review of Financial Studies 6(2), 1993, 327-343; A. Elices, Models with time-dependent
/// parameters using transform methods: application to Heston's model, http://arxiv.org/pdf/0708.2020.
/// </para>
/// </remarks>
public sealed class PiecewiseTimeDependentHestonModel : CalibratedModel
{
    /// <summary>Initializes a new instance of the <see cref="PiecewiseTimeDependentHestonModel"/> class.</summary>
    /// <param name="riskFreeRate">The risk-free curve.</param>
    /// <param name="dividendYield">The dividend curve.</param>
    /// <param name="s0">The spot.</param>
    /// <param name="v0">The variance's value.</param>
    /// <param name="theta">The variance's long-run mean.</param>
    /// <param name="kappa">The variance's mean-reversion speed.</param>
    /// <param name="sigma">The variance's volatility.</param>
    /// <param name="rho">The correlation.</param>
    /// <param name="timeGrid">The parameters' time grid, on the model's clock.</param>
    /// <exception cref="ArgumentNullException">A curve, a parameter or the time grid is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException"><paramref name="v0"/> isn't positive.</exception>
    public PiecewiseTimeDependentHestonModel(
        YieldTermStructure riskFreeRate,
        YieldTermStructure dividendYield,
        double s0,
        double v0,
        Parameter theta,
        Parameter kappa,
        Parameter sigma,
        Parameter rho,
        TimeGrid timeGrid)
        : base(5)
    {
        ArgumentNullException.ThrowIfNull(riskFreeRate);
        ArgumentNullException.ThrowIfNull(dividendYield);
        ArgumentNullException.ThrowIfNull(timeGrid);
        S0 = s0;
        RiskFreeRate = riskFreeRate;
        DividendYield = dividendYield;
        TimeGrid = timeGrid;
        SetArgument(0, theta);
        SetArgument(1, kappa);
        SetArgument(2, sigma);
        SetArgument(3, rho);
        SetArgument(4, new ConstantParameter(v0, new PositiveConstraint(), nameof(v0)));
    }

    /// <summary>Gets the variance's value v₀.</summary>
    public double V0 => Arguments[4].Evaluate(0.0);

    /// <summary>Gets the spot.</summary>
    public double S0 { get; }

    /// <summary>Gets the parameters' time grid, on the model's clock.</summary>
    public TimeGrid TimeGrid { get; }

    /// <summary>Gets the dividend curve.</summary>
    public YieldTermStructure DividendYield { get; }

    /// <summary>Gets the risk-free curve.</summary>
    public YieldTermStructure RiskFreeRate { get; }

    /// <summary>The variance's long-run mean at a time.</summary>
    /// <param name="t">The time, on the model's clock.</param>
    /// <returns>θ(t).</returns>
    public double Theta(double t) => Arguments[0].Evaluate(t);

    /// <summary>The variance's mean-reversion speed at a time.</summary>
    /// <param name="t">The time, on the model's clock.</param>
    /// <returns>κ(t).</returns>
    public double Kappa(double t) => Arguments[1].Evaluate(t);

    /// <summary>The variance's volatility at a time.</summary>
    /// <param name="t">The time, on the model's clock.</param>
    /// <returns>σ(t).</returns>
    public double Sigma(double t) => Arguments[2].Evaluate(t);

    /// <summary>The correlation at a time.</summary>
    /// <param name="t">The time, on the model's clock.</param>
    /// <returns>ρ(t).</returns>
    public double Rho(double t) => Arguments[3].Evaluate(t);

    // The model's time to a date: the risk-free curve's year fraction from the earlier curve date, none before it;
    // QuantLib's time, the risk-free curve's own, where the dividend curve isn't dated before it.
    internal double ModelTime(Date date)
    {
        Date origin = EarliestReferenceDate();
        double t = origin == RiskFreeRate.ReferenceDate
            ? RiskFreeRate.DayCounter.YearFraction(RiskFreeRate.ReferenceDate, date)
            : RiskFreeRate.DayCounter.YearFraction(origin, date);
        return Math.Max(t, 0.0);
    }

    // The model's clock, whose times ModelTime gives: the risk-free curve where the dividend curve isn't dated before
    // it, else a flat curve on the earlier curve date and the risk-free curve's day counter.
    internal TermStructure ModelClock()
    {
        Date origin = EarliestReferenceDate();
        return origin == RiskFreeRate.ReferenceDate ? RiskFreeRate : new FlatForward(origin, 0.0, RiskFreeRate.DayCounter);
    }

    private Date EarliestReferenceDate() =>
        DividendYield.ReferenceDate < RiskFreeRate.ReferenceDate ? DividendYield.ReferenceDate : RiskFreeRate.ReferenceDate;
}
