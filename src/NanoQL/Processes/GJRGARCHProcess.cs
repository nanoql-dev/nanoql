// SPDX-FileCopyrightText: Copyright (C) 2008 Yee Man Chan
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/processes/gjrgarchprocess.hpp ql/processes/gjrgarchprocess.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;
using Vector = NanoQL.Numerics.Vector;

namespace NanoQL.Processes;

/// <summary>
/// The GJR-GARCH(1,1) process in its continuous-time limit: dS = μS dt + √v S dW₁ and
/// dv = (ω + (β + αq₂ + γq₃ − 1)v) dt + (ασ₁₂ + γσ₁₃)v dW₁ + √(α²(σ₂² − σ₁₂²) + γ²(σ₃² − σ₁₃²) + 2αγ(σ₂₃ − σ₁₂σ₁₃)) v dW₂,
/// with the moments of the news shocks at the market price of risk λ, the daily parameters scaled by the days per year.
/// Its state is the underlying and the annualized variance.
/// </summary>
/// <remarks>
/// <para>
/// Immutable: the curves, the spot and the parameters are fixed at construction, where QuantLib's process holds handles
/// and a quote; the shocks' moments, which depend on λ, α, γ and the days per year only, are computed once there by
/// QuantLib's arithmetic, where QuantLib's process recomputes them in each call. The in-place
/// <see cref="Evolve(double, ReadOnlySpan{double}, double, ReadOnlySpan{double}, Span{double})"/> steps by QuantLib's
/// arithmetic without allocating, its rates read without building QuantLib's rate objects, so its bits are the
/// allocating form's (the S-hes plan's HR2).
/// </para>
/// <para>
/// The model's time runs from the earliest reference date of the two curves on the risk-free curve's day counter, as
/// <see cref="HestonProcess"/>'s does, where QuantLib's runs from the risk-free curve's date and its analytic engine's
/// day count turns negative before a later-dated curve (HR7 (xviii)).
/// </para>
/// <para>
/// Reference: Glosten, L., Jagannathan, R., Runkle, D., 1993. Relationship between the expected value and the volatility
/// of the nominal excess return on stocks. Journal of Finance 48, 1779-1801.
/// </para>
/// </remarks>
public sealed class GJRGARCHProcess : StochasticProcess
{
    private readonly YieldTermStructure riskFreeRate;
    private readonly YieldTermStructure dividendYield;
    private readonly GJRGARCHProcessDiscretization scheme;

    // QuantLib's q2 and q3, the step's correlation coefficients rho1 and rho2 (gjrgarchprocess.cpp:125-141), and rho2's
    // two factors, which the diffusion multiplies in its own order (:102-105)
    private readonly double q2;
    private readonly double q3;
    private readonly double rho1;
    private readonly double rho2;
    private readonly double sqrtDaysPerYear;
    private readonly double shockRoot;

    // the Monte Carlo engine's clock (OnClock): null for the risk-free curve's own times, QuantLib's
    private TermStructure? clock;

    /// <summary>Initializes a new instance of the <see cref="GJRGARCHProcess"/> class.</summary>
    /// <param name="riskFreeRate">The risk-free curve.</param>
    /// <param name="dividendYield">The dividend curve.</param>
    /// <param name="s0">The underlying's value.</param>
    /// <param name="v0">The daily variance's value.</param>
    /// <param name="omega">The daily variance's constant ω.</param>
    /// <param name="alpha">The news shock's coefficient α.</param>
    /// <param name="beta">The variance's persistence β.</param>
    /// <param name="gamma">The leverage coefficient γ.</param>
    /// <param name="lambda">The market price of risk λ.</param>
    /// <param name="daysPerYear">The days per year the daily parameters are scaled by.</param>
    /// <param name="d">The discretization.</param>
    /// <exception cref="ArgumentNullException">A curve is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="d"/> isn't a member of its type.</exception>
    public GJRGARCHProcess(
        YieldTermStructure riskFreeRate,
        YieldTermStructure dividendYield,
        double s0,
        double v0,
        double omega,
        double alpha,
        double beta,
        double gamma,
        double lambda,
        double daysPerYear = 252.0,
        GJRGARCHProcessDiscretization d = GJRGARCHProcessDiscretization.FullTruncation)
        : base(new EulerDiscretization())
    {
        ArgumentNullException.ThrowIfNull(riskFreeRate);
        ArgumentNullException.ThrowIfNull(dividendYield);
        EnumArguments.Check(d, nameof(d));
        this.riskFreeRate = riskFreeRate;
        this.dividendYield = dividendYield;
        S0 = s0;
        V0 = v0;
        Omega = omega;
        Alpha = alpha;
        Beta = beta;
        Gamma = gamma;
        Lambda = lambda;
        DaysPerYear = daysPerYear;
        scheme = d;

        (double n, double cn) = ShockDensities(lambda);
        double sigma2 = 2.0 + (4.0 * lambda * lambda);
        q2 = 1.0 + (lambda * lambda);
        q3 = (lambda * n) + cn + (lambda * lambda * cn);
        double emlE4 = (lambda * lambda * lambda * n) + (5.0 * lambda * n)
            + (3.0 * cn) + (lambda * lambda * lambda * lambda * cn)
            + (6.0 * lambda * lambda * cn);
        double sigma3 = emlE4 - (q3 * q3);
        double sigma12 = -2.0 * lambda;
        double sigma13 = (-2.0 * n) - (2 * lambda * cn);
        double sigma23 = (2.0 * cn) + (sigma12 * sigma13);
        sqrtDaysPerYear = Math.Sqrt(daysPerYear);
        shockRoot = Math.Sqrt((alpha * alpha * (sigma2 - (sigma12 * sigma12)))
                              + (gamma * gamma * (sigma3 - (sigma13 * sigma13)))
                              + (2.0 * alpha * gamma * (sigma23 - (sigma12 * sigma13))));
        rho1 = sqrtDaysPerYear * ((alpha * sigma12) + (gamma * sigma13));
        rho2 = sqrtDaysPerYear * shockRoot;
    }

    /// <inheritdoc/>
    public override int Size => 2;

    /// <summary>Gets the underlying's value.</summary>
    public double S0 { get; }

    /// <summary>Gets the daily variance's value.</summary>
    public double V0 { get; }

    /// <summary>Gets the daily variance's constant ω.</summary>
    public double Omega { get; }

    /// <summary>Gets the news shock's coefficient α.</summary>
    public double Alpha { get; }

    /// <summary>Gets the variance's persistence β.</summary>
    public double Beta { get; }

    /// <summary>Gets the leverage coefficient γ.</summary>
    public double Gamma { get; }

    /// <summary>Gets the market price of risk λ.</summary>
    public double Lambda { get; }

    /// <summary>Gets the days per year the daily parameters are scaled by.</summary>
    public double DaysPerYear { get; }

    /// <summary>Gets the risk-free curve.</summary>
    public YieldTermStructure RiskFreeRate => riskFreeRate;

    /// <summary>Gets the dividend curve.</summary>
    public YieldTermStructure DividendYield => dividendYield;

    /// <inheritdoc/>
    public override Vector InitialValues() => new([S0, DaysPerYear * V0]);

    /// <inheritdoc/>
    public override void InitialValues(Span<double> result)
    {
        result[0] = S0;
        result[1] = DaysPerYear * V0;
    }

    /// <inheritdoc/>
    public override Vector Drift(double t, Vector x)
    {
        ArgumentNullException.ThrowIfNull(x);
        double vol = x[1] > 0.0 ? Math.Sqrt(x[1])
            : scheme == GJRGARCHProcessDiscretization.Reflection ? -Math.Sqrt(-x[1])
            : 0.0;

        return new Vector(
        [
            RiskFreeForwardRate(t, t) - DividendRate(t, t) - (0.5 * vol * vol),
            (DaysPerYear * DaysPerYear * Omega) + (DaysPerYear * (Beta + (Alpha * q2) + (Gamma * q3) - 1.0)
                * (scheme == GJRGARCHProcessDiscretization.PartialTruncation ? x[1] : vol * vol)),
        ]);
    }

    /// <inheritdoc/>
    public override Matrix Diffusion(double t, Vector x)
    {
        ArgumentNullException.ThrowIfNull(x);

        // the correlation matrix is | 1 rho; rho 1 |, whose square root (which is used here) is | 1 0; rho sqrt(1 - rho^2) |
        var tmp = new Matrix(2, 2);
        double vol = x[1] > 0.0 ? Math.Sqrt(x[1])
            : scheme == GJRGARCHProcessDiscretization.Reflection ? -Math.Sqrt(-x[1])
            : 1e-8; // set vol to (almost) zero but still expose some correlation information

        // the coefficients of dW_1 and dW_2 in the asset return's and the variance's processes
        tmp[0, 0] = vol;
        tmp[0, 1] = 0.0;
        tmp[1, 0] = rho1 * vol * vol;
        tmp[1, 1] = vol * vol * sqrtDaysPerYear * shockRoot;
        return tmp;
    }

    /// <inheritdoc/>
    public override Vector Apply(Vector x0, Vector dx)
    {
        ArgumentNullException.ThrowIfNull(x0);
        ArgumentNullException.ThrowIfNull(dx);
        return new Vector([x0[0] * Math.Exp(dx[0]), x0[1] + dx[1]]);
    }

    /// <inheritdoc/>
    public override Vector Evolve(double t0, Vector x0, double dt, Vector dw)
    {
        ArgumentNullException.ThrowIfNull(x0);
        ArgumentNullException.ThrowIfNull(dw);
        var retVal = new Vector(2);
        Step(x0.AsSpan(), dt, dw.AsSpan(), retVal.AsSpan(), RiskFreeForwardRate(t0, t0 + dt) - DividendRate(t0, t0 + dt));
        return retVal;
    }

    /// <inheritdoc/>
    /// <remarks>QuantLib's arithmetic without allocating, so the values are
    /// <see cref="Evolve(double, Vector, double, Vector)"/>'s bit for bit.</remarks>
    public override void Evolve(double t0, ReadOnlySpan<double> x0, double dt, ReadOnlySpan<double> dw, Span<double> result) =>
        Step(x0, dt, dw, result, ForwardDriftInPlace(t0, dt));

    /// <summary>The time of a date: the risk-free curve's year fraction from its reference date, QuantLib's.</summary>
    /// <param name="date">The date.</param>
    /// <returns>The time.</returns>
    /// <exception cref="ArgumentException"><paramref name="date"/> is <c>default(Date)</c>.</exception>
    public override double Time(Date date)
    {
        Date.ThrowIfDefault(date, nameof(date));
        return clock is not null
            ? clock.TimeFromReference(date)
            : riskFreeRate.DayCounter.YearFraction(riskFreeRate.ReferenceDate, date);
    }

    // The earliest reference date of the two curves, HestonProcess's (HR7 (xviii)).
    internal Date EarliestReferenceDate() =>
        dividendYield.ReferenceDate < riskFreeRate.ReferenceDate ? dividendYield.ReferenceDate : riskFreeRate.ReferenceDate;

    // The model's time to a date: the risk-free curve's year fraction from the earliest reference date, none before it;
    // QuantLib's time, the risk-free curve's own, where the dividend curve isn't dated before it (HR7 (xviii)).
    internal double ModelTime(Date date)
    {
        Date origin = EarliestReferenceDate();
        double t = origin == riskFreeRate.ReferenceDate
            ? riskFreeRate.DayCounter.YearFraction(riskFreeRate.ReferenceDate, date)
            : riskFreeRate.DayCounter.YearFraction(origin, date);
        return Math.Max(t, 0.0);
    }

    // The model's clock, whose times ModelTime gives: the risk-free curve where the dividend curve isn't dated before
    // it, else a flat curve on the earliest reference date and the risk-free curve's day counter.
    internal TermStructure ModelClock()
    {
        Date origin = EarliestReferenceDate();
        return origin == riskFreeRate.ReferenceDate ? riskFreeRate : new FlatForward(origin, 0.0, riskFreeRate.DayCounter);
    }

    // The process on the Monte Carlo engine's clock, HestonProcess's form: a copy whose times are the clock's and whose
    // curves are read at the instants those times denote; the process itself where the clock shares the risk-free
    // curve's times.
    internal GJRGARCHProcess OnClock(TermStructure clock)
    {
        if (clock.SharesTimes(riskFreeRate))
        {
            return this;
        }

        var copy = (GJRGARCHProcess)MemberwiseClone();
        copy.clock = clock;
        return copy;
    }

    // the standard normal's density and distribution at the market price of risk
    private static (double Density, double Distribution) ShockDensities(double lambda) =>
        (Math.Exp(-lambda * lambda / 2.0) / Math.Sqrt(2 * Math.PI), new CumulativeNormalDistribution().Evaluate(lambda));

    // QuantLib's evolve, the forward drift r - q over [t0, t0 + dt] given (gjrgarchprocess.cpp:119-188)
    private void Step(ReadOnlySpan<double> x0, double dt, ReadOnlySpan<double> dw, Span<double> retVal, double drift)
    {
        double vol, mu, nu;
        double sdt = Math.Sqrt(dt);

        // For the definition of PartialTruncation, FullTruncation and Reflection see Lord, R., R. Koekkoek and D. van
        // Dijk (2006), "A Comparison of biased simulation schemes for stochastic volatility models", Working Paper,
        // Tinbergen Institute
        switch (scheme)
        {
            case GJRGARCHProcessDiscretization.PartialTruncation:
                vol = x0[1] > 0.0 ? Math.Sqrt(x0[1]) : 0.0;
                mu = drift - (0.5 * vol * vol);
                nu = (DaysPerYear * DaysPerYear * Omega) + (DaysPerYear * (Beta + (Alpha * q2) + (Gamma * q3) - 1.0) * x0[1]);

                retVal[0] = x0[0] * Math.Exp((mu * dt) + (vol * dw[0] * sdt));
                retVal[1] = x0[1] + (nu * dt) + (sdt * vol * vol * ((rho1 * dw[0]) + (rho2 * dw[1])));
                break;
            case GJRGARCHProcessDiscretization.FullTruncation:
                vol = x0[1] > 0.0 ? Math.Sqrt(x0[1]) : 0.0;
                mu = drift - (0.5 * vol * vol);
                nu = (DaysPerYear * DaysPerYear * Omega) + (DaysPerYear * (Beta + (Alpha * q2) + (Gamma * q3) - 1.0) * vol * vol);

                retVal[0] = x0[0] * Math.Exp((mu * dt) + (vol * dw[0] * sdt));
                retVal[1] = x0[1] + (nu * dt) + (sdt * vol * vol * ((rho1 * dw[0]) + (rho2 * dw[1])));
                break;
            default:
                vol = Math.Sqrt(Math.Abs(x0[1]));
                mu = drift - (0.5 * vol * vol);
                nu = (DaysPerYear * DaysPerYear * Omega) + (DaysPerYear * (Beta + (Alpha * q2) + (Gamma * q3) - 1.0) * vol * vol);

                retVal[0] = x0[0] * Math.Exp((mu * dt) + (vol * dw[0] * sdt));
                retVal[1] = (vol * vol) + (nu * dt) + (sdt * vol * vol * ((rho1 * dw[0]) + (rho2 * dw[1])));
                break;
        }
    }

    // r - q over [t0, t0 + dt] without QuantLib's rate objects (YieldTermStructure.ContinuousForwardRate, the same
    // arithmetic), for the in-place step
    private double ForwardDriftInPlace(double t0, double dt)
    {
        double t1 = t0 + dt;
        double r = clock is not null
            ? riskFreeRate.ContinuousForwardRateAt(clock, t0, t1)
            : riskFreeRate.ContinuousForwardRate(t0, t1);
        double q = clock is not null
            ? dividendYield.ContinuousForwardRateAt(clock, t0, t1)
            : dividendYield.SharesTimes(riskFreeRate)
                ? dividendYield.ContinuousForwardRate(t0, t1)
                : dividendYield.ContinuousForwardRateAt(riskFreeRate, t0, t1);
        return r - q;
    }

    // r over [t1, t2]: QuantLib's read, or on the Monte Carlo engine's clock at the instants its times denote
    private double RiskFreeForwardRate(double t1, double t2) =>
        clock is not null
            ? riskFreeRate.ContinuousForwardRateAt(clock, t1, t2)
            : riskFreeRate.ForwardRate(t1, t2, Compounding.Continuous).Rate;

    // q over [t1, t2]: QuantLib's read where the dividend curve shares the risk-free curve's times, otherwise at the
    // instants they denote (HestonProcess's reads); on the Monte Carlo engine's clock at the instants its times denote
    private double DividendRate(double t1, double t2) =>
        clock is not null
            ? dividendYield.ContinuousForwardRateAt(clock, t1, t2)
            : dividendYield.SharesTimes(riskFreeRate)
                ? dividendYield.ForwardRate(t1, t2, Compounding.Continuous).Rate
                : dividendYield.ContinuousForwardRateAt(riskFreeRate, t1, t2);
}
