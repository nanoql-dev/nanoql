// SPDX-FileCopyrightText: Copyright (C) 2001, 2002, 2003 Sadruddin Rejeb
// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006, 2007, 2009 StatPro Italia srl
// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: Copyright (C) 2004, 2005, 2006, 2007 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/processes/blackscholesprocess.hpp ql/processes/blackscholesprocess.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Processes;

/// <summary>
/// The generalized Black-Scholes stochastic process, d ln S(t) = (r(t) − q(t) − σ(t, S)²/2) dt + σ dW_t, on a
/// risk-free curve, a dividend curve and a Black volatility, with its local volatility σ(t, S) derived from the Black
/// one. The interface is expressed in S; the internal calculations work on ln S.
/// </summary>
/// <remarks>
/// <para>
/// An extension point, as in QuantLib: its four named processes derive from it. The process is immutable; the curves
/// and volatilities it holds have reference dates fixed when they were built, so <see cref="Time"/> needs no context.
/// </para>
/// <para>
/// The local volatility is chosen in the constructor, by QuantLib's type tests in QuantLib's order: a
/// <see cref="BlackConstantVol"/> gives a <see cref="LocalConstantVol"/> at its volatility at the spot, a
/// <see cref="BlackVarianceCurve"/> a <see cref="LocalVolCurve"/>, and any other structure a
/// <see cref="LocalVolSurface"/> at the spot. The first two are strike-independent, so the expectation, the variance
/// and the evolution are exact; on a surface they are the discretization's. QuantLib chooses and caches it at the
/// first use. A process given its local volatility uses it, and is never strike-independent.
/// </para>
/// </remarks>
public class GeneralizedBlackScholesProcess : StochasticProcess1D
{
    private readonly double x0;
    private readonly YieldTermStructure riskFreeRate;
    private readonly YieldTermStructure dividendYield;
    private readonly BlackVolTermStructure blackVolatility;
    private readonly bool forceDiscretization;
    private readonly LocalVolTermStructure localVolatility;
    private readonly bool isStrikeIndependent;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeneralizedBlackScholesProcess"/> class, its local volatility
    /// derived from its Black volatility.
    /// </summary>
    /// <param name="x0">The underlying's value.</param>
    /// <param name="dividendTS">The dividend curve.</param>
    /// <param name="riskFreeTS">The risk-free curve.</param>
    /// <param name="blackVolTS">The Black volatility.</param>
    /// <param name="d">The discretization, or <see langword="null"/> for QuantLib's default, Euler's.</param>
    /// <param name="forceDiscretization">
    /// Whether to use the discretization even where the local volatility is strike-independent.
    /// </param>
    /// <exception cref="ArgumentNullException">A curve or the volatility is <see langword="null"/>.</exception>
    /// <exception cref="NanoQLException">The Black volatility can't be read at time 0 and the spot.</exception>
    public GeneralizedBlackScholesProcess(
        double x0,
        YieldTermStructure dividendTS,
        YieldTermStructure riskFreeTS,
        BlackVolTermStructure blackVolTS,
        StochasticProcess1DDiscretization? d = null,
        bool forceDiscretization = false)
        : base(d ?? new EulerDiscretization())
    {
        ArgumentNullException.ThrowIfNull(dividendTS);
        ArgumentNullException.ThrowIfNull(riskFreeTS);
        ArgumentNullException.ThrowIfNull(blackVolTS);
        this.x0 = x0;
        riskFreeRate = riskFreeTS;
        dividendYield = dividendTS;
        blackVolatility = blackVolTS;
        this.forceDiscretization = forceDiscretization;
        (localVolatility, isStrikeIndependent) = DerivedLocalVolatility(x0, dividendTS, riskFreeTS, blackVolTS);
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GeneralizedBlackScholesProcess"/> class with a given local
    /// volatility, and Euler's discretization.
    /// </summary>
    /// <param name="x0">The underlying's value.</param>
    /// <param name="dividendTS">The dividend curve.</param>
    /// <param name="riskFreeTS">The risk-free curve.</param>
    /// <param name="blackVolTS">The Black volatility.</param>
    /// <param name="localVolTS">The local volatility.</param>
    /// <exception cref="ArgumentNullException">A curve or a volatility is <see langword="null"/>.</exception>
    public GeneralizedBlackScholesProcess(
        double x0,
        YieldTermStructure dividendTS,
        YieldTermStructure riskFreeTS,
        BlackVolTermStructure blackVolTS,
        LocalVolTermStructure localVolTS)
        : base(new EulerDiscretization())
    {
        ArgumentNullException.ThrowIfNull(dividendTS);
        ArgumentNullException.ThrowIfNull(riskFreeTS);
        ArgumentNullException.ThrowIfNull(blackVolTS);
        ArgumentNullException.ThrowIfNull(localVolTS);
        this.x0 = x0;
        riskFreeRate = riskFreeTS;
        dividendYield = dividendTS;
        blackVolatility = blackVolTS;
        forceDiscretization = false;
        localVolatility = localVolTS;
        isStrikeIndependent = false;
    }

    /// <inheritdoc/>
    public override double X0 => x0;

    /// <summary>Gets the underlying's value: QuantLib's state variable, a quote there and a number here.</summary>
    public double StateVariable => x0;

    /// <summary>Gets the dividend curve.</summary>
    public YieldTermStructure DividendYield => dividendYield;

    /// <summary>Gets the risk-free curve.</summary>
    public YieldTermStructure RiskFreeRate => riskFreeRate;

    /// <summary>Gets the Black volatility.</summary>
    public BlackVolTermStructure BlackVolatility => blackVolatility;

    // The earliest reference date of the risk-free curve, the dividend curve and the Black volatility: the date the
    // engines' times run from, each structure accruing nothing before its own (the user's rulings of 2026-10-21 on the
    // S-eq plan, C4R2-O2 (i) and C4R3-O2 (a)); the risk-free curve's where none is dated before it.
    internal Date EarliestReferenceDate()
    {
        Date origin = riskFreeRate.ReferenceDate;
        if (dividendYield.ReferenceDate < origin)
        {
            origin = dividendYield.ReferenceDate;
        }

        if (blackVolatility.ReferenceDate < origin)
        {
            origin = blackVolatility.ReferenceDate;
        }

        return origin;
    }

    /// <summary>Gets the local volatility: the one given, or the one derived from the Black volatility.</summary>
    public LocalVolTermStructure LocalVolatility => localVolatility;

    /// <summary>The drift of ln S: r − q − σ²/2, the rates instantaneous forwards over 0.0001 years.</summary>
    /// <param name="t">The time.</param>
    /// <param name="x">The underlying's value.</param>
    /// <returns>The drift.</returns>
    /// <exception cref="NanoQLException">A curve or the local volatility can't be read there.</exception>
    public override double Drift(double t, double x)
    {
        double sigma = Diffusion(t, x);

        // we could be more anticipatory if we know the right dt
        // for which the drift will be used
        double t1 = t + 0.0001;
        return riskFreeRate.ContinuousForwardRate(t, t1, true)
            - DividendRate(t, t1)
            - (0.5 * sigma * sigma);
    }

    /// <summary>
    /// The diffusion: the local volatility at the time and value, extrapolated; on another clock than the risk-free
    /// curve's, at the instant's own time, per unit of the risk-free curve's time, and nothing before its reference
    /// date.
    /// </summary>
    /// <param name="t">The time.</param>
    /// <param name="x">The underlying's value.</param>
    /// <returns>The diffusion.</returns>
    /// <exception cref="NanoQLException">The local volatility can't be read there.</exception>
    public override double Diffusion(double t, double x)
    {
        if (localVolatility.SharesTimes(riskFreeRate))
        {
            return localVolatility.LocalVol(t, x, true);
        }

        double scale = localVolatility.AccruedPerClockTime(riskFreeRate, t, t);
        return localVolatility.LocalVol(localVolatility.AccrualTime(riskFreeRate, t), x, true) * Math.Sqrt(scale);
    }

    /// <summary>Applies a change of ln S: x0 eᵈˣ.</summary>
    /// <param name="x0">The underlying's value.</param>
    /// <param name="dx">The change of its logarithm.</param>
    /// <returns>The changed value.</returns>
    public override double Apply(double x0, double dx) => x0 * Math.Exp(dx);

    /// <summary>
    /// The expectation of S after a time interval, exactly, from the forward rates over it; only where the local
    /// volatility is strike-independent and the discretization isn't forced.
    /// </summary>
    /// <param name="t0">The start of the interval.</param>
    /// <param name="x0">The underlying's value at its start.</param>
    /// <param name="dt">The interval's length.</param>
    /// <returns>The expectation.</returns>
    /// <exception cref="NanoQLException">
    /// The local volatility depends on the strike, or the discretization is forced: "not implemented", as QuantLib's.
    /// </exception>
    /// <remarks>QuantLib's warning: it should return E(S), not exp(E(log S)).</remarks>
    public override double Expectation(double t0, double x0, double dt)
    {
        if (isStrikeIndependent && !forceDiscretization)
        {
            // exact value for curves
            return x0 * Math.Exp(dt * (riskFreeRate.ContinuousForwardRate(t0, t0 + dt, true) - DividendRate(t0, t0 + dt)));
        }
        else
        {
            throw new NanoQLException("not implemented");
        }
    }

    /// <summary>
    /// The standard deviation after a time interval: the square root of the exact variance where the local volatility
    /// is strike-independent and the discretization isn't forced, the discretization's otherwise.
    /// </summary>
    /// <param name="t0">The start of the interval.</param>
    /// <param name="x0">The underlying's value at its start.</param>
    /// <param name="dt">The interval's length.</param>
    /// <returns>The standard deviation.</returns>
    public override double StdDeviation(double t0, double x0, double dt)
    {
        if (isStrikeIndependent && !forceDiscretization)
        {
            // exact value for curves
            return Math.Sqrt(Variance(t0, x0, dt));
        }
        else
        {
            return Discretization1D().Diffusion(this, t0, x0, dt);
        }
    }

    /// <summary>
    /// The variance after a time interval: the Black variance's increment over it, at a strike of 0.01, where the
    /// local volatility is strike-independent and the discretization isn't forced; the discretization's otherwise.
    /// </summary>
    /// <param name="t0">The start of the interval.</param>
    /// <param name="x0">The underlying's value at its start.</param>
    /// <param name="dt">The interval's length.</param>
    /// <returns>The variance.</returns>
    public override double Variance(double t0, double x0, double dt)
    {
        if (isStrikeIndependent && !forceDiscretization)
        {
            // exact value for curves; on another clock than the risk-free curve's, accrued between the instants
            return blackVolatility.SharesTimes(riskFreeRate)
                ? blackVolatility.BlackVariance(t0 + dt, 0.01) - blackVolatility.BlackVariance(t0, 0.01)
                : blackVolatility.BlackForwardVarianceAt(riskFreeRate, t0, t0 + dt, 0.01);
        }
        else
        {
            return Discretization1D().Variance(this, t0, x0, dt);
        }
    }

    /// <summary>
    /// The value after a time interval and a Brownian increment: exactly where the local volatility is
    /// strike-independent and the discretization isn't forced, by the discretization otherwise.
    /// </summary>
    /// <param name="t0">The start of the interval.</param>
    /// <param name="x0">The underlying's value at its start.</param>
    /// <param name="dt">The interval's length.</param>
    /// <param name="dw">The Brownian increment.</param>
    /// <returns>The value.</returns>
    public override double Evolve(double t0, double x0, double dt, double dw)
    {
        if (isStrikeIndependent && !forceDiscretization)
        {
            // exact value for curves
            double var = Variance(t0, x0, dt);
            double drift = ((riskFreeRate.ContinuousForwardRate(t0, t0 + dt, true) - DividendRate(t0, t0 + dt)) * dt)
                - (0.5 * var);
            return Apply(x0, (Math.Sqrt(var) * dw) + drift);
        }
        else
        {
            return Apply(x0, Discretization1D().Drift(this, t0, x0, dt) + (StdDeviation(t0, x0, dt) * dw));
        }
    }

    /// <summary>The time of a date, by the risk-free curve's day counter from its reference date.</summary>
    /// <param name="date">The date.</param>
    /// <returns>The time.</returns>
    /// <exception cref="ArgumentException"><paramref name="date"/> is <c>default(Date)</c>.</exception>
    public override double Time(Date date)
    {
        Date.ThrowIfDefault(date, nameof(date));
        return riskFreeRate.DayCounter.YearFraction(riskFreeRate.ReferenceDate, date);
    }

    // The process on an engine's clock (the S-van plan's VR1 (v)): its times, its Time(date)'s among them, on the
    // clock's day counter from the clock's reference date, and each structure, the risk-free curve among them, read at
    // the instants those times denote, accruing nothing before its own reference date (TermStructure.AccrualTime); the
    // process itself where the clock shares the risk-free curve's times, so a market on that clock keeps QuantLib's
    // bits. The Monte Carlo engines' paths run on FdmBlackScholesOp.Clock, whose origin is the earliest structure's
    // reference date.
    internal StochasticProcess1D OnClock(TermStructure clock) =>
        clock.SharesTimes(riskFreeRate) ? this : new ClockedView(this, clock);

    // q over [t1, t2] on the risk-free curve's clock: QuantLib's read where the dividend curve shares its times;
    // otherwise at the instants they denote, nothing accruing before the curve's reference date (the user's rulings of
    // 2026-10-21 on the S-eq plan, C4-O2 and C4R2-O2; TermStructure.AccrualTime)
    private double DividendRate(double t1, double t2) =>
        dividendYield.SharesTimes(riskFreeRate)
            ? dividendYield.ContinuousForwardRate(t1, t2, true)
            : dividendYield.ContinuousForwardRateAt(riskFreeRate, t1, t2, true);

    // QuantLib's localVolatility(): the local volatility by the Black volatility's type, and whether it is
    // strike-independent.
    private static (LocalVolTermStructure LocalVol, bool StrikeIndependent) DerivedLocalVolatility(
        double x0, YieldTermStructure dividendTS, YieldTermStructure riskFreeTS, BlackVolTermStructure blackVolTS)
    {
        // constant Black vol?
        if (blackVolTS is BlackConstantVol constVol)
        {
            // ok, the local vol is constant too.
            return (
                new LocalConstantVol(constVol.ReferenceDate, constVol.BlackVol(0.0, x0), constVol.DayCounter),
                true);
        }

        // ok, so it's not constant. Maybe it's strike-independent?
        if (blackVolTS is BlackVarianceCurve volCurve)
        {
            // ok, we can use the optimized algorithm
            return (new LocalVolCurve(volCurve), true);
        }

        // ok, so it's strike-dependent. Never mind.
        return (new LocalVolSurface(blackVolTS, riskFreeTS, dividendTS, x0), false);
    }

    // The process read on another clock than the risk-free curve's (OnClock): each member QuantLib's formula, with the
    // rates, the variance and the local volatility read at the instants the clock's times denote.
    private sealed class ClockedView : StochasticProcess1D
    {
        private readonly GeneralizedBlackScholesProcess p;
        private readonly TermStructure clock;

        public ClockedView(GeneralizedBlackScholesProcess p, TermStructure clock)
            : base(p.Discretization1D())
        {
            this.p = p;
            this.clock = clock;
        }

        public override double X0 => p.x0;

        public override double Drift(double t, double x)
        {
            double sigma = Diffusion(t, x);
            double t1 = t + 0.0001;
            return p.riskFreeRate.ContinuousForwardRateAt(clock, t, t1, true)
                - p.dividendYield.ContinuousForwardRateAt(clock, t, t1, true)
                - (0.5 * sigma * sigma);
        }

        public override double Diffusion(double t, double x)
        {
            LocalVolTermStructure localVol = p.localVolatility;
            if (localVol.SharesTimes(clock))
            {
                return localVol.LocalVol(t, x, true);
            }

            double scale = localVol.AccruedPerClockTime(clock, t, t);
            return localVol.LocalVol(localVol.AccrualTime(clock, t), x, true) * Math.Sqrt(scale);
        }

        public override double Apply(double x0, double dx) => x0 * Math.Exp(dx);

        public override double Expectation(double t0, double x0, double dt)
        {
            if (p.isStrikeIndependent && !p.forceDiscretization)
            {
                return x0 * Math.Exp(dt * (
                    p.riskFreeRate.ContinuousForwardRateAt(clock, t0, t0 + dt, true)
                    - p.dividendYield.ContinuousForwardRateAt(clock, t0, t0 + dt, true)));
            }
            else
            {
                throw new NanoQLException("not implemented");
            }
        }

        public override double StdDeviation(double t0, double x0, double dt) =>
            p.isStrikeIndependent && !p.forceDiscretization
                ? Math.Sqrt(Variance(t0, x0, dt))
                : Discretization1D().Diffusion(this, t0, x0, dt);

        public override double Variance(double t0, double x0, double dt) =>
            p.isStrikeIndependent && !p.forceDiscretization
                ? p.blackVolatility.BlackForwardVarianceAt(clock, t0, t0 + dt, 0.01)
                : Discretization1D().Variance(this, t0, x0, dt);

        public override double Evolve(double t0, double x0, double dt, double dw)
        {
            if (p.isStrikeIndependent && !p.forceDiscretization)
            {
                double var = Variance(t0, x0, dt);
                double drift = ((p.riskFreeRate.ContinuousForwardRateAt(clock, t0, t0 + dt, true)
                    - p.dividendYield.ContinuousForwardRateAt(clock, t0, t0 + dt, true))
                    * dt) - (0.5 * var);
                return Apply(x0, (Math.Sqrt(var) * dw) + drift);
            }
            else
            {
                return Apply(x0, Discretization1D().Drift(this, t0, x0, dt) + (StdDeviation(t0, x0, dt) * dw));
            }
        }

        public override double Time(Date date) => clock.TimeFromReference(date);
    }
}
