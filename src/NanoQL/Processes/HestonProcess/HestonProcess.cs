// SPDX-FileCopyrightText: Copyright (C) 2005, 2007, 2009, 2014 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/processes/hestonprocess.hpp ql/processes/hestonprocess.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Numerics;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;
using Vector = NanoQL.Numerics.Vector;

namespace NanoQL.Processes;

/// <summary>
/// The Heston square-root stochastic-volatility process: dS = μS dt + √v S dW₁, dv = κ(θ − v) dt + σ√v dW₂, with
/// dW₁dW₂ = ρ dt. Its state is the underlying and the variance.
/// </summary>
/// <remarks>
/// <para>
/// Immutable: the curves, the spot and the parameters are fixed at construction, where QuantLib's process holds handles
/// and a quote. <see cref="Evolve(double, Vector, double, Vector)"/> steps by the chosen
/// <see cref="HestonProcessDiscretization"/>; the exact schemes sample the variance from its non-central chi-squared
/// law, whose inverse refuses where its series doesn't converge, and Broadie and Kaya's schemes also sample the
/// integrated variance.
/// </para>
/// <para>
/// An extension point, as in QuantLib: <see cref="BatesProcess"/> derives from it. The in-place
/// <see cref="Evolve(double, ReadOnlySpan{double}, double, ReadOnlySpan{double}, Span{double})"/> steps the truncation,
/// reflection and quadratic-exponential schemes without allocating, by QuantLib's arithmetic, its rates read without
/// building QuantLib's rate objects, so its bits are the allocating form's; the exact schemes' samplers allocate, and
/// step through the allocating form.
/// </para>
/// </remarks>
public class HestonProcess : StochasticProcess
{
    private static readonly GaussLaguerreIntegration GaussLaguerre128 = new(128);

    // the standard normal's cumulative distribution, which QuantLib's steps build afresh
    private static readonly CumulativeNormalDistribution CumNormal = new();

    // Pdf's inversion: its Gauss-Legendre panels, and their number at most
    private static readonly GaussLegendreIntegration GaussLegendre32 = new(32);
    private const int MaxInversionPanels = 100000;

    private readonly YieldTermStructure riskFreeRate;
    private readonly YieldTermStructure dividendYield;
    private readonly HestonProcessDiscretization scheme;

    // The Monte Carlo engines' clock (OnClock): null for the risk-free curve's own times, QuantLib's.
    private TermStructure? clock;

    /// <summary>Initializes a new instance of the <see cref="HestonProcess"/> class.</summary>
    /// <param name="riskFreeRate">The risk-free curve.</param>
    /// <param name="dividendYield">The dividend curve.</param>
    /// <param name="s0">The underlying's value.</param>
    /// <param name="v0">The variance's value.</param>
    /// <param name="kappa">The variance's mean-reversion speed κ.</param>
    /// <param name="theta">The variance's long-run mean θ.</param>
    /// <param name="sigma">The variance's volatility σ.</param>
    /// <param name="rho">The correlation ρ of the two Brownian motions.</param>
    /// <param name="d">The discretization <see cref="Evolve(double, Vector, double, Vector)"/> steps by.</param>
    /// <exception cref="ArgumentNullException">A curve is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="d"/> isn't a member of its enum.</exception>
    public HestonProcess(
        YieldTermStructure riskFreeRate,
        YieldTermStructure dividendYield,
        double s0,
        double v0,
        double kappa,
        double theta,
        double sigma,
        double rho,
        HestonProcessDiscretization d = HestonProcessDiscretization.QuadraticExponentialMartingale)
        : base(new EulerDiscretization())
    {
        ArgumentNullException.ThrowIfNull(riskFreeRate);
        ArgumentNullException.ThrowIfNull(dividendYield);
        EnumArguments.Check(d, nameof(d));
        this.riskFreeRate = riskFreeRate;
        this.dividendYield = dividendYield;
        S0 = s0;
        V0 = v0;
        Kappa = kappa;
        Theta = theta;
        Sigma = sigma;
        Rho = rho;
        scheme = d;
    }

    /// <inheritdoc/>
    public override int Size => 2;

    /// <inheritdoc/>
    /// <remarks>Three for Broadie and Kaya's schemes, which sample the integrated variance with the third; two
    /// otherwise.</remarks>
    public override int Factors =>
        scheme is HestonProcessDiscretization.BroadieKayaExactSchemeLobatto
            or HestonProcessDiscretization.BroadieKayaExactSchemeTrapezoidal
            or HestonProcessDiscretization.BroadieKayaExactSchemeLaguerre
            ? 3
            : 2;

    /// <summary>Gets the underlying's value.</summary>
    public double S0 { get; }

    /// <summary>Gets the variance's value.</summary>
    public double V0 { get; }

    /// <summary>Gets the correlation ρ.</summary>
    public double Rho { get; }

    /// <summary>Gets the mean-reversion speed κ.</summary>
    public double Kappa { get; }

    /// <summary>Gets the long-run mean θ.</summary>
    public double Theta { get; }

    /// <summary>Gets the variance's volatility σ.</summary>
    public double Sigma { get; }

    /// <summary>Gets the dividend curve.</summary>
    public YieldTermStructure DividendYield => dividendYield;

    /// <summary>Gets the risk-free curve.</summary>
    public YieldTermStructure RiskFreeRate => riskFreeRate;

    /// <inheritdoc/>
    public override Vector InitialValues() => new([S0, V0]);

    /// <inheritdoc/>
    public override void InitialValues(Span<double> result)
    {
        result[0] = S0;
        result[1] = V0;
    }

    // The earliest reference date of the risk-free and dividend curves: the date the model's variance accrues from in
    // the engines that read the model by date, each curve accruing nothing before its own; the risk-free curve's where
    // the dividend curve isn't dated before it.
    internal Date EarliestReferenceDate() =>
        dividendYield.ReferenceDate < riskFreeRate.ReferenceDate ? dividendYield.ReferenceDate : riskFreeRate.ReferenceDate;

    // The model's time to a date: the risk-free curve's year fraction from the earliest reference date, none before it;
    // QuantLib's time, the risk-free curve's own, where the dividend curve isn't dated before it.
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

    // The structure whose times the process's are: the engine's clock on a copy OnClock made, the risk-free curve
    // otherwise.
    internal TermStructure TimeClock => clock ?? riskFreeRate;

    // The process on an engine's clock: a copy, of the same type, whose times, Time(date)'s among them, are the
    // clock's, and whose curves are read at the instants those times denote, each accruing nothing before its own
    // reference date (TermStructure.AccrualTime); the process itself where the clock shares the risk-free curve's
    // times, so a market on that clock keeps QuantLib's bits. The variance's dynamics run in the clock's time.
    internal HestonProcess OnClock(TermStructure clock)
    {
        if (clock.SharesTimes(riskFreeRate))
        {
            return this;
        }

        var copy = (HestonProcess)MemberwiseClone();
        copy.clock = clock;
        return copy;
    }

    /// <inheritdoc/>
    public override Vector Drift(double t, Vector x)
    {
        ArgumentNullException.ThrowIfNull(x);
        double vol = x[1] > 0.0 ? Math.Sqrt(x[1])
            : scheme == HestonProcessDiscretization.Reflection ? -Math.Sqrt(-x[1])
            : 0.0;

        return new Vector(
        [
            RiskFreeForwardRate(t, t)
                - DividendRate(t, t)
                - (0.5 * vol * vol),
            Kappa * (Theta - (scheme == HestonProcessDiscretization.PartialTruncation ? x[1] : vol * vol)),
        ]);
    }

    /// <inheritdoc/>
    /// <remarks>The square root of the correlation matrix times the volatilities: [[√v, 0], [ρσ√v, √(1 − ρ²)σ√v]], a
    /// variance at or below zero read as 1e-8 (Reflection's as its negative root), so the correlation still
    /// shows.</remarks>
    public override Matrix Diffusion(double t, Vector x)
    {
        ArgumentNullException.ThrowIfNull(x);
        var tmp = new Matrix(2, 2);
        double vol = x[1] > 0.0 ? Math.Sqrt(x[1])
            : scheme == HestonProcessDiscretization.Reflection ? -Math.Sqrt(-x[1])
            : 1e-8; // set vol to (almost) zero but still expose some correlation information
        double sigma2 = Sigma * vol;
        double sqrhov = Math.Sqrt(1.0 - (Rho * Rho));

        tmp[0, 0] = vol;
        tmp[0, 1] = 0.0;
        tmp[1, 0] = Rho * sigma2;
        tmp[1, 1] = sqrhov * sigma2;
        return tmp;
    }

    /// <inheritdoc/>
    /// <remarks>The underlying moves by its exponential, the variance additively.</remarks>
    public override Vector Apply(Vector x0, Vector dx)
    {
        ArgumentNullException.ThrowIfNull(x0);
        ArgumentNullException.ThrowIfNull(dx);
        return new Vector([x0[0] * Math.Exp(dx[0]), x0[1] + dx[1]]);
    }

    /// <summary>
    /// The joint density of the log spot <paramref name="x"/> and the variance <paramref name="v"/> at time
    /// <paramref name="t"/>: the variance's non-central chi-squared density times the log spot's conditional density
    /// given it, from the integrated variance's characteristic function (Broadie and Kaya).
    /// </summary>
    /// <param name="x">The log spot.</param>
    /// <param name="v">The variance.</param>
    /// <param name="t">The time, on the risk-free curve's clock.</param>
    /// <param name="eps">QuantLib's accuracy for a tail search whose result its own code overwrites
    /// (<c>hestonprocess.cpp:307-323</c>): the density depends on it neither there nor here.</param>
    /// <returns>The density.</returns>
    /// <remarks>
    /// <para>
    /// The dividend curve is read at the instant <paramref name="t"/> denotes on the risk-free curve's clock, nothing
    /// accrued before its own reference date (the mismatched-dates class); QuantLib's read, the dividend curve's own
    /// time <paramref name="t"/>, where the two curves share their times.
    /// </para>
    /// <para>
    /// QuantLib's density doesn't hold its digits (<c>hestonprocess.cpp:300-334</c>). It integrates over the integrated
    /// variance up to twice a Cornish-Fisher quantile whose skew and kurtosis are finite differences of the
    /// characteristic function at a step of 1e-2, which rest on its last bits, and that cut-off can fall where the
    /// integrand is still a fifth of its peak (10% low on ordinary inputs); and its 128-point Gauss-Laguerre rule for
    /// the conditional density reaches u ≈ 500, where at short times the characteristic function hasn't decayed (NaN
    /// and negative densities at a quarter of a year). Here the log spot's conditional density given the variance is
    /// the inversion of its characteristic function: given the integrated variance ξ the log spot is normal, with mean
    /// x₀ + a − cξ and variance (1 − ρ²)ξ, c = ½ − ρκ/σ, as QuantLib's integrand has it, so its characteristic function
    /// given the variance is e^{iu(x₀ + a)}·Φ(−uc + i·½u²(1 − ρ²)), Φ the integrated variance's, with the Bessel ratio
    /// in its exponentially weighted form. The inversion integral runs by 32-point Gauss-Legendre panels no wider than
    /// the integrand's oscillation and decay allow, out to where E[e^{−½u²(1 − ρ²)ξ}], which bounds the integrand,
    /// falls below 1e-17. A documented divergence (<c>Divergences/HestonPdfTests.cs</c>), checked by the log spot's
    /// marginal, the density integrated over the variance, against the log spot's density from Heston's characteristic
    /// function, which QuantLib's analytic Heston engine prices by. Where the variance's density is 0 the density is 0.
    /// The value's absolute accuracy is about 1e-13, the inversion's rounding floor: far from the density's mass it is
    /// noise of that size, of either sign.
    /// </para>
    /// </remarks>
    /// <exception cref="NanoQLException">Boost's non-central chi-squared refuses its arguments; |ρ| is 1, where the log
    /// spot's conditional density has no variance; or the inversion doesn't converge within 100,000 panels.</exception>
    public double Pdf(double x, double v, double t, double eps = 1e-3)
    {
        _ = eps;
        double k = Sigma * Sigma * (1 - Math.Exp(-Kappa * t)) / (4 * Kappa);
        double a = Math.Log(dividendYield.DiscountAt(riskFreeRate, t) / riskFreeRate.Discount(t))
            + (Rho / Sigma * (v - V0 - (Kappa * Theta * t)));
        double x0 = Math.Log(S0);

        double df0 = 4 * Theta * Kappa / (Sigma * Sigma);
        double ncp = 4 * Kappa * Math.Exp(-Kappa * t) / (Sigma * Sigma * (1 - Math.Exp(-Kappa * t))) * V0;
        NonCentralChiSquaredFunctions.NonCentralChiSquaredDistribution(df0, ncp);
        double variance = NonCentralChiSquaredFunctions.Pdf(df0, ncp, v / k) / k;
        if (variance == 0.0)
        {
            return 0.0;
        }

        double damping = 0.5 * (1 - (Rho * Rho));
        if (!(damping > 0.0))
        {
            throw new NanoQLException("the log spot's conditional density has no variance at |rho| = 1");
        }

        double c = 0.5 - (Rho * Kappa / Sigma);
        double y = x - x0 - a;

        // the scale of the integrated variance, and from it the integrand's frequency and decay
        double xiScale = 2.0 * t * Max(V0, Max(v, Theta));
        double frequency = Math.Abs(y) + (Math.Abs(c) * xiScale);
        double decay = 1.0 / Math.Sqrt(damping * xiScale);
        double maxWidth = Min(50.0, Min(decay, 20.0 / frequency));

        // Re[e^{-iuy}·Φ(ζ(u))], ζ(u) = -uc + i·½u²(1 − ρ²)
        double Integrand(double u) =>
            (Complex.Exp(new Complex(0.0, -u * y)) * Phi(new Complex(-u * c, damping * u * u), V0, v, t, true)).Real;

        double sum = 0.0, lo = 0.0, width = Min(2.0, maxWidth);
        for (int panel = 0; ; panel++)
        {
            if (panel == MaxInversionPanels)
            {
                throw new NanoQLException("the log spot's conditional characteristic function doesn't decay within the inversion's panels");
            }

            double from = lo, half = 0.5 * width;
            sum += half * GaussLegendre32.Evaluate(s => Integrand(from + (half * (s + 1))));
            lo += width;

            // E[exp(-½u²(1 − ρ²)ξ)] = Φ(i·½u²(1 − ρ²)) bounds |Φ(ζ(u))| and falls with u
            if (Phi(new Complex(0.0, damping * lo * lo), V0, v, t, true).Real < 1e-17)
            {
                break;
            }

            width = Min(width * 1.2, maxWidth);
        }

        return sum / Math.PI * variance;
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">
    /// The quadratic-exponential martingale correction has no solution ("illegal value"), or the variance's law can't
    /// be inverted.
    /// </exception>
    public override Vector Evolve(double t0, Vector x0, double dt, Vector dw)
    {
        ArgumentNullException.ThrowIfNull(x0);
        ArgumentNullException.ThrowIfNull(dw);
        return EvolveHeston(t0, x0, dt, dw);
    }

    // QuantLib's HestonProcess::evolve, non-virtual, so a derived process's in-place step can fall back to it alone
    private Vector EvolveHeston(double t0, Vector x0, double dt, Vector dw)
    {
        var retVal = new Vector(2);
        double vol, vol2, mu, nu, dy;

        double sdt = Math.Sqrt(dt);
        double sqrhov = Math.Sqrt(1.0 - (Rho * Rho));

        switch (scheme)
        {
            // For the definition of PartialTruncation, FullTruncation
            // and Reflection  see Lord, R., R. Koekkoek and D. van Dijk (2006),
            // "A Comparison of biased simulation schemes for
            //  stochastic volatility models",
            // Working Paper, Tinbergen Institute
            case HestonProcessDiscretization.PartialTruncation:
                vol = x0[1] > 0.0 ? Math.Sqrt(x0[1]) : 0.0;
                vol2 = Sigma * vol;
                mu = ForwardDrift(t0, dt) - (0.5 * vol * vol);
                nu = Kappa * (Theta - x0[1]);

                retVal[0] = x0[0] * Math.Exp((mu * dt) + (vol * dw[0] * sdt));
                retVal[1] = x0[1] + (nu * dt) + (vol2 * sdt * ((Rho * dw[0]) + (sqrhov * dw[1])));
                break;
            case HestonProcessDiscretization.FullTruncation:
                vol = x0[1] > 0.0 ? Math.Sqrt(x0[1]) : 0.0;
                vol2 = Sigma * vol;
                mu = ForwardDrift(t0, dt) - (0.5 * vol * vol);
                nu = Kappa * (Theta - (vol * vol));

                retVal[0] = x0[0] * Math.Exp((mu * dt) + (vol * dw[0] * sdt));
                retVal[1] = x0[1] + (nu * dt) + (vol2 * sdt * ((Rho * dw[0]) + (sqrhov * dw[1])));
                break;
            case HestonProcessDiscretization.Reflection:
                vol = Math.Sqrt(Math.Abs(x0[1]));
                vol2 = Sigma * vol;
                mu = ForwardDrift(t0, dt) - (0.5 * vol * vol);
                nu = Kappa * (Theta - (vol * vol));

                retVal[0] = x0[0] * Math.Exp((mu * dt) + (vol * dw[0] * sdt));
                retVal[1] = (vol * vol) + (nu * dt) + (vol2 * sdt * ((Rho * dw[0]) + (sqrhov * dw[1])));
                break;
            case HestonProcessDiscretization.NonCentralChiSquareVariance:
                // use Alan Lewis trick to decorrelate the equity and the variance
                // process by using y(t)=x(t)-\frac{rho}{sigma}\nu(t)
                // and Ito's Lemma. Then use exact sampling for the variance
                // process. For further details please read the Wilmott thread
                // "QuantLib code is very high quality"
                vol = x0[1] > 0.0 ? Math.Sqrt(x0[1]) : 0.0;
                mu = ForwardDrift(t0, dt) - (0.5 * vol * vol);

                retVal[1] = VarianceDistribution(x0[1], dw[1], dt);
                dy = ((mu - (Rho / Sigma * Kappa * (Theta - (vol * vol)))) * dt) + (vol * sqrhov * dw[0] * sdt);

                retVal[0] = x0[0] * Math.Exp(dy + (Rho / Sigma * (retVal[1] - x0[1])));
                break;
            case HestonProcessDiscretization.QuadraticExponential:
            case HestonProcessDiscretization.QuadraticExponentialMartingale:
                EvolveQuadraticExponential(t0, x0.AsSpan(), dt, dw.AsSpan(), retVal.AsSpan(), false);
                break;
            case HestonProcessDiscretization.BroadieKayaExactSchemeLobatto:
            case HestonProcessDiscretization.BroadieKayaExactSchemeLaguerre:
            case HestonProcessDiscretization.BroadieKayaExactSchemeTrapezoidal:
            {
                double nu_0 = x0[1];
                double nu_t = VarianceDistribution(nu_0, dw[1], dt);

                double x = Min(1.0 - NumericLimits.QL_EPSILON, Max(0.0, new CumulativeNormalDistribution().Evaluate(dw[2])));

                double vds = new Brent().Solve(
                    xi => CdfNuDs(xi, nu_0, nu_t, dt, scheme) - x, 1e-5, Theta * dt, 0.1 * Theta * dt);

                double vdw = (nu_t - nu_0 - (Kappa * Theta * dt) + (Kappa * vds)) / Sigma;

                mu = (ForwardDrift(t0, dt) * dt) - (0.5 * vds) + (Rho * vdw);

                double sig = Math.Sqrt((1 - (Rho * Rho)) * vds);
                double s = x0[0] * Math.Exp(mu + (sig * dw[0]));

                retVal[0] = s;
                retVal[1] = nu_t;
                break;
            }

            default:
                throw new NanoQLException("unknown discretization schema");
        }

        return retVal;
    }

    /// <inheritdoc/>
    /// <remarks>The truncation, reflection and quadratic-exponential schemes step without allocating, by QuantLib's
    /// arithmetic, so the values are <see cref="Evolve(double, Vector, double, Vector)"/>'s bit for bit; the exact
    /// schemes step through it.</remarks>
    /// <exception cref="NanoQLException">
    /// The quadratic-exponential martingale correction has no solution ("illegal value"), or the variance's law can't
    /// be inverted.
    /// </exception>
    public override void Evolve(double t0, ReadOnlySpan<double> x0, double dt, ReadOnlySpan<double> dw, Span<double> result)
    {
        double vol, vol2, mu, nu;

        double sdt = Math.Sqrt(dt);
        double sqrhov = Math.Sqrt(1.0 - (Rho * Rho));

        switch (scheme)
        {
            case HestonProcessDiscretization.PartialTruncation:
                vol = x0[1] > 0.0 ? Math.Sqrt(x0[1]) : 0.0;
                vol2 = Sigma * vol;
                mu = ForwardDriftInPlace(t0, dt) - (0.5 * vol * vol);
                nu = Kappa * (Theta - x0[1]);

                result[0] = x0[0] * Math.Exp((mu * dt) + (vol * dw[0] * sdt));
                result[1] = x0[1] + (nu * dt) + (vol2 * sdt * ((Rho * dw[0]) + (sqrhov * dw[1])));
                break;
            case HestonProcessDiscretization.FullTruncation:
                vol = x0[1] > 0.0 ? Math.Sqrt(x0[1]) : 0.0;
                vol2 = Sigma * vol;
                mu = ForwardDriftInPlace(t0, dt) - (0.5 * vol * vol);
                nu = Kappa * (Theta - (vol * vol));

                result[0] = x0[0] * Math.Exp((mu * dt) + (vol * dw[0] * sdt));
                result[1] = x0[1] + (nu * dt) + (vol2 * sdt * ((Rho * dw[0]) + (sqrhov * dw[1])));
                break;
            case HestonProcessDiscretization.Reflection:
                vol = Math.Sqrt(Math.Abs(x0[1]));
                vol2 = Sigma * vol;
                mu = ForwardDriftInPlace(t0, dt) - (0.5 * vol * vol);
                nu = Kappa * (Theta - (vol * vol));

                result[0] = x0[0] * Math.Exp((mu * dt) + (vol * dw[0] * sdt));
                result[1] = (vol * vol) + (nu * dt) + (vol2 * sdt * ((Rho * dw[0]) + (sqrhov * dw[1])));
                break;
            case HestonProcessDiscretization.QuadraticExponential:
            case HestonProcessDiscretization.QuadraticExponentialMartingale:
                EvolveQuadraticExponential(t0, x0, dt, dw, result, true);
                break;
            default:
                EvolveHeston(t0, new Vector(x0), dt, new Vector(dw)).AsSpan().CopyTo(result);
                break;
        }
    }

    /// <summary>The time of a date: the risk-free curve's year fraction from its reference date.</summary>
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

    // std::max and std::min
    private static double Max(double a, double b) => a < b ? b : a;

    private static double Min(double a, double b) => b < a ? b : a;

    private static double Squared(double x) => x * x;

    // This is the continuous version of a characteristic function
    // for the exact sampling of the Heston process, s. page 8, formula 13,
    // M. Broadie, O. Kaya, Exact Simulation of Stochastic Volatility and
    // other Affine Jump Diffusion Processes
    // http://finmath.stanford.edu/seminars/documents/Broadie.pdf
    //
    // This version does not need a branch correction procedure.
    // For details please see:
    // Roger Lord, "Efficient Pricing Algorithms for exotic Derivatives",
    // http://repub.eur.nl/pub/13917/LordR-Thesis.pdf
    private Complex Phi(Complex a, double nu_0, double nu_t, double dt, bool weightedBessel = false)
    {
        double theta = Theta;
        double kappa = Kappa;
        double sigma = Sigma;

        double sigma2 = sigma * sigma;
        Complex ga = Complex.Sqrt((kappa * kappa) - (2 * sigma2 * a * new Complex(0.0, 1.0)));
        double d = 4 * theta * kappa / sigma2;
        double nu = (0.5 * d) - 1;
        Complex z = ga * Complex.Exp(-0.5 * ga * dt) / (1.0 - Complex.Exp(-ga * dt));
        Complex log_z = (-0.5 * ga * dt) + Complex.Log(ga / (1.0 - Complex.Exp(-ga * dt)));

        Complex alpha = 4.0 * ga * Complex.Exp(-0.5 * ga * dt) / (sigma2 * (1.0 - Complex.Exp(-ga * dt)));
        double beta = 4.0 * kappa * Math.Exp(-0.5 * kappa * dt) / (sigma2 * (1.0 - Math.Exp(-kappa * dt)));

        return ga * Complex.Exp(-0.5 * (ga - kappa) * dt) * (1 - Math.Exp(-kappa * dt))
            / (kappa * (1.0 - Complex.Exp(-ga * dt)))
            * Complex.Exp((nu_0 + nu_t) / sigma2 * ((kappa * (1.0 + Math.Exp(-kappa * dt)) / (1.0 - Math.Exp(-kappa * dt)))
                - (ga * (1.0 + Complex.Exp(-ga * dt)) / (1.0 - Complex.Exp(-ga * dt)))))
            * Complex.Exp(nu * log_z) / Complex.Pow(z, nu)
            * (nu_t > 1e-8
                ? (weightedBessel
                    ? BesselRatio(nu, Math.Sqrt(nu_0 * nu_t) * alpha, Math.Sqrt(nu_0 * nu_t) * beta)
                    : ModifiedBesselFunctions.ModifiedBesselFunction_i(nu, Math.Sqrt(nu_0 * nu_t) * alpha)
                        / ModifiedBesselFunctions.ModifiedBesselFunction_i(nu, Math.Sqrt(nu_0 * nu_t) * beta))
                : Complex.Pow(alpha / beta, nu));
    }

    // I_nu(za)/I_nu(zb) through the exponentially weighted functions, I_nu(z)·e^(−z), which don't overflow where the
    // ratio's terms do at short times (Pdf's form; Evolve keeps QuantLib's)
    private static Complex BesselRatio(double nu, Complex za, Complex zb) =>
        ModifiedBesselFunctions.ModifiedBesselFunction_i_exponentiallyWeighted(nu, za)
        / ModifiedBesselFunctions.ModifiedBesselFunction_i_exponentiallyWeighted(nu, zb)
        * Complex.Exp(za - zb);

    private double Ch(double x, double u, double nu_0, double nu_t, double dt) =>
        MathConstants.M_2_PI * Math.Sin(u * x) / u * Phi(u, nu_0, nu_t, dt).Real;

    private double CornishFisherEps(double nu_0, double nu_t, double dt, double eps)
    {
        // use moment generating function to get the
        // first,second, third and fourth moment of the distribution
        const double d = 1e-2;
        double p2 = Phi(new Complex(0, -2 * d), nu_0, nu_t, dt).Real;
        double p1 = Phi(new Complex(0, -d), nu_0, nu_t, dt).Real;
        double p0 = Phi(new Complex(0, 0), nu_0, nu_t, dt).Real;
        double pm1 = Phi(new Complex(0, d), nu_0, nu_t, dt).Real;
        double pm2 = Phi(new Complex(0, 2 * d), nu_0, nu_t, dt).Real;

        double avg = (pm2 - (8 * pm1) + (8 * p1) - p2) / (12 * d);
        double m2 = (-pm2 + (16 * pm1) - (30 * p0) + (16 * p1) - p2) / (12 * d * d);
        double var = m2 - (avg * avg);
        double stdDev = Math.Sqrt(var);

        double m3 = ((-0.5 * pm2) + pm1 - p1 + (0.5 * p2)) / (d * d * d);
        double skew = (m3 - (3 * var * avg) - (avg * avg * avg)) / (var * stdDev);

        double m4 = (pm2 - (4 * pm1) + (6 * p0) - (4 * p1) + p2) / (d * d * d * d);
        double kurt = (m4 - (4 * m3 * avg) + (6 * m2 * avg * avg) - (3 * avg * avg * avg * avg)) / (var * var);

        // Cornish-Fisher relation to come up with an improved
        // estimate of 1-F(u_\eps) < \eps
        double q = new InverseCumulativeNormal().Evaluate(1 - eps);
        double w = q + ((q * q) - 1) / 6 * skew + (((q * q * q) - (3 * q)) / 24 * (kurt - 3))
            - (((2 * q * q * q) - (5 * q)) / 36 * skew * skew);

        return avg + (w * stdDev);
    }

    private double CdfNuDs(double x, double nu_0, double nu_t, double dt, HestonProcessDiscretization method)
    {
        const double eps = 1e-4;
        double u_eps = Min(100.0, Max(0.1, CornishFisherEps(nu_0, nu_t, dt, eps)));

        switch (method)
        {
            case HestonProcessDiscretization.BroadieKayaExactSchemeLaguerre:
            {
                // get the upper bound for the integration
                double upper = u_eps / 2.0;
                while (Complex.Abs(Phi(upper, nu_0, nu_t, dt) / upper) > eps)
                {
                    upper *= 2.0;
                }

                return x < upper
                    ? Max(0.0, Min(1.0, GaussLaguerre128.Evaluate(u => Ch(x, u, nu_0, nu_t, dt))))
                    : 1.0;
            }

            case HestonProcessDiscretization.BroadieKayaExactSchemeLobatto:
            {
                // get the upper bound for the integration
                double upper = u_eps / 2.0;
                while (Complex.Abs(Phi(upper, nu_0, nu_t, dt) / upper) > eps)
                {
                    upper *= 2.0;
                }

                return x < upper
                    ? Max(
                        0.0,
                        Min(
                            1.0,
                            new GaussLobattoIntegral(int.MaxValue, eps).Integrate(
                                xi => Ch(x, xi, nu_0, nu_t, dt), NumericLimits.QL_EPSILON, upper)))
                    : 1.0;
            }

            case HestonProcessDiscretization.BroadieKayaExactSchemeTrapezoidal:
            {
                const double h = 0.05;

                double si = ExponentialIntegralsFunctions.Si(0.5 * h * x);
                double s = MathConstants.M_2_PI * si;
                Complex f;
                int j = 0;
                do
                {
                    ++j;
                    double u = h * j;
                    double si_n = ExponentialIntegralsFunctions.Si(x * (u + (0.5 * h)));

                    f = Phi(u, nu_0, nu_t, dt);
                    s += MathConstants.M_2_PI * f.Real * (si_n - si);
                    si = si_n;
                }
                while (MathConstants.M_2_PI * Complex.Abs(f) / j > eps);

                return s;
            }

            default:
                throw new NanoQLException("unknown integration method");
        }
    }

    // inPlace reads the drift's rates without building QuantLib's rate objects, the same bits
    private void EvolveQuadraticExponential(
        double t0, ReadOnlySpan<double> x0, double dt, ReadOnlySpan<double> dw, Span<double> retVal, bool inPlace)
    {
        // for details of the quadratic exponential discretization scheme
        // see Leif Andersen,
        // Efficient Simulation of the Heston Stochastic Volatility Model
        double ex = Math.Exp(-Kappa * dt);

        double m = Theta + ((x0[1] - Theta) * ex);
        double s2 = (x0[1] * Sigma * Sigma * ex / Kappa * (1 - ex))
            + (Theta * Sigma * Sigma / (2 * Kappa) * (1 - ex) * (1 - ex));
        double psi = s2 / (m * m);

        const double g1 = 0.5;
        const double g2 = 0.5;
        double k0 = -Rho * Kappa * Theta * dt / Sigma;
        double k1 = (g1 * dt * ((Kappa * Rho / Sigma) - 0.5)) - (Rho / Sigma);
        double k2 = (g2 * dt * ((Kappa * Rho / Sigma) - 0.5)) + (Rho / Sigma);
        double k3 = g1 * dt * (1 - (Rho * Rho));
        double k4 = g2 * dt * (1 - (Rho * Rho));
        double A = k2 + (0.5 * k4);

        if (psi < 1.5)
        {
            double b2 = (2 / psi) - 1 + Math.Sqrt(2 / psi * ((2 / psi) - 1));
            double b = Math.Sqrt(b2);
            double a = m / (1 + b2);

            if (scheme == HestonProcessDiscretization.QuadraticExponentialMartingale)
            {
                // martingale correction
                if (!(A < 1 / (2 * a)))
                {
                    throw new NanoQLException("illegal value");
                }

                k0 = (-A * b2 * a / (1 - (2 * A * a))) + (0.5 * Math.Log(1 - (2 * A * a))) - ((k1 + (0.5 * k3)) * x0[1]);
            }

            retVal[1] = a * (b + dw[1]) * (b + dw[1]);
        }
        else
        {
            double p = (psi - 1) / (psi + 1);
            double beta = (1 - p) / m;
            double u = CumNormal.Evaluate(dw[1]);

            if (scheme == HestonProcessDiscretization.QuadraticExponentialMartingale)
            {
                // martingale correction
                if (!(A < beta))
                {
                    throw new NanoQLException("illegal value");
                }

                k0 = -Math.Log(p + (beta * (1 - p) / (beta - A))) - ((k1 + (0.5 * k3)) * x0[1]);
            }

            retVal[1] = u <= p ? 0.0 : Math.Log((1 - p) / (1 - u)) / beta;
        }

        double mu = inPlace ? ForwardDriftInPlace(t0, dt) : ForwardDrift(t0, dt);

        retVal[0] = x0[0] * Math.Exp((mu * dt) + k0 + (k1 * x0[1]) + (k2 * retVal[1])
            + (Math.Sqrt((k3 * x0[1]) + (k4 * retVal[1])) * dw[0]));
    }

    // r − q over [t0, t0 + dt], continuously compounded
    private double ForwardDrift(double t0, double dt) =>
        RiskFreeForwardRate(t0, t0 + dt) - DividendRate(t0, t0 + dt);

    // ForwardDrift's value without QuantLib's rate objects (YieldTermStructure.ContinuousForwardRate, the same
    // arithmetic), for the in-place step
    internal double ForwardDriftInPlace(double t0, double dt)
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
    internal double RiskFreeForwardRate(double t1, double t2) =>
        clock is not null
            ? riskFreeRate.ContinuousForwardRateAt(clock, t1, t2)
            : riskFreeRate.ForwardRate(t1, t2, Compounding.Continuous).Rate;

    // q over [t1, t2] on the risk-free curve's clock: QuantLib's read where the dividend curve shares its times;
    // otherwise at the instants they denote, nothing accruing before the curve's reference date
    // (TermStructure.AccrualTime); on the Monte Carlo engine's clock at the instants its times denote
    internal double DividendRate(double t1, double t2) =>
        clock is not null
            ? dividendYield.ContinuousForwardRateAt(clock, t1, t2)
            : dividendYield.SharesTimes(riskFreeRate)
                ? dividendYield.ForwardRate(t1, t2, Compounding.Continuous).Rate
                : dividendYield.ContinuousForwardRateAt(riskFreeRate, t1, t2);

    private double VarianceDistribution(double v, double dw, double dt)
    {
        double df = 4 * Theta * Kappa / (Sigma * Sigma);
        double ncp = 4 * Kappa * Math.Exp(-Kappa * dt) / (Sigma * Sigma * (1 - Math.Exp(-Kappa * dt))) * v;

        double p = Min(1.0 - NumericLimits.QL_EPSILON, Max(0.0, new CumulativeNormalDistribution().Evaluate(dw)));

        return Sigma * Sigma * (1 - Math.Exp(-Kappa * dt)) / (4 * Kappa)
            * new InverseNonCentralCumulativeChiSquareDistribution(df, ncp, 100).Evaluate(p);
    }
}
