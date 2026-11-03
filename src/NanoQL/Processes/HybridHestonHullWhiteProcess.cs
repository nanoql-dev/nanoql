// SPDX-FileCopyrightText: Copyright (C) 2007, 2008 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/processes/hybridhestonhullwhiteprocess.hpp ql/processes/hybridhestonhullwhiteprocess.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Models;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;
using Vector = NanoQL.Numerics.Vector;

namespace NanoQL.Processes;

/// <summary>
/// The three-factor hybrid of a Heston equity and a Hull-White short rate in its forward measure: its state is the
/// underlying, its variance and the short rate, the equity correlated with the short rate.
/// </summary>
/// <remarks>
/// <para>
/// Immutable over its Heston process and its forward Hull-White process. The Hull-White process the hybrid evolves, its
/// model and its numeraire's end discount are on the Heston process's risk-free curve, from the given process's
/// reversion, volatility and forward-measure time, which <see cref="HullWhiteProcess"/> returns; the given process's
/// own curve isn't read. QuantLib builds its model on the Heston curve but takes the short rate from the given
/// process's curve (<c>hybridhestonhullwhiteprocess.cpp:37-46</c>, <c>:64</c>, <c>:137</c>, <c>:179</c>), so the numeraire
/// prices a bond on one curve at a rate taken from the other; here all of it runs on the one curve, QuantLib's bits
/// where the two curves are one.
/// </para>
/// <para>
/// The process's times run on the Heston process's model clock, from the earliest reference date of its curves on the
/// risk-free curve's day counter, each curve read at the instant a time denotes, the Hull-White parts on the risk-free
/// curve restated on that clock; QuantLib's process reads the dividend curve at the risk-free curve's times.
/// </para>
/// <para>
/// The step's terms m₂, m₄, m₅, v₁ and v₁₂, QuantLib's expressions, cancel as a·dt falls: below a·dt of
/// <see cref="StepCut"/> they are evaluated in double-double, below its floor in quad-double and below that by the
/// cubic in a through four quad-double values, QuantLib's double and bits from the cut up, the cut where QuantLib's
/// double first departs from the expressions' exact values by more than 1e-12 relative, measured at the run. The
/// in-place <see cref="Evolve(double, ReadOnlySpan{double}, double, ReadOnlySpan{double}, Span{double})"/> steps by the
/// same arithmetic without allocating.
/// </para>
/// </remarks>
public sealed class HybridHestonHullWhiteProcess : StochasticProcess
{
    /// <summary>The a·dt below which the step's terms run in double-double, measured at the run.</summary>
    internal const double StepCut = 1e-1;

    // the a·dt below which double-double and quad-double cancel past 1e-12 relative, measured at the run
    private const double StepFloor = 5e-7;
    private const double StepQuadFloor = 1e-12;

    private readonly HestonProcess hestonProcess;
    private readonly HestonProcess hestonOnClock;
    private readonly HullWhiteForwardProcess hullWhiteProcess;
    private readonly HullWhite hullWhiteModel;
    private readonly YieldTermStructure riskFree;
    private readonly YieldTermStructure dividends;
    private readonly TermStructure clock;
    private readonly double corrEquityShortRate;
    private readonly HybridHestonHullWhiteProcessDiscretization scheme;
    private readonly double maxRho;
    private readonly double forwardMeasureTime;
    private readonly double endDiscount;

    /// <summary>Initializes a new instance of the <see cref="HybridHestonHullWhiteProcess"/> class.</summary>
    /// <param name="hestonProcess">The Heston process.</param>
    /// <param name="hullWhiteProcess">The forward Hull-White process, whose reversion, volatility and forward-measure
    /// time are read.</param>
    /// <param name="corrEquityShortRate">The correlation of the equity with the short rate.</param>
    /// <param name="discretization">The discretization.</param>
    /// <exception cref="ArgumentNullException">A process is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="discretization"/> isn't a member of its
    /// type.</exception>
    /// <exception cref="ArgumentException">The correlations' squares sum past 1 ("correlation matrix is not positive
    /// definite"), or the Hull-White volatility isn't positive ("positive vol of Hull White process is
    /// required").</exception>
    /// <exception cref="NanoQLException">The Hull-White process has no forward-measure time.</exception>
    public HybridHestonHullWhiteProcess(
        HestonProcess hestonProcess,
        HullWhiteForwardProcess hullWhiteProcess,
        double corrEquityShortRate,
        HybridHestonHullWhiteProcessDiscretization discretization = HybridHestonHullWhiteProcessDiscretization.BSMHullWhite)
    {
        ArgumentNullException.ThrowIfNull(hestonProcess);
        ArgumentNullException.ThrowIfNull(hullWhiteProcess);
        EnumArguments.Check(discretization, nameof(discretization));
        this.hestonProcess = hestonProcess;
        clock = hestonProcess.ModelClock();
        hestonOnClock = hestonProcess.OnClock(clock);
        riskFree = clock.SharesTimes(hestonProcess.RiskFreeRate)
            ? hestonProcess.RiskFreeRate
            : new YieldTermStructureOnClock(hestonProcess.RiskFreeRate, clock);
        dividends = hestonProcess.DividendYield;
        forwardMeasureTime = hullWhiteProcess.ForwardMeasureTime;
        this.hullWhiteProcess = new HullWhiteForwardProcess(riskFree, hullWhiteProcess.A, hullWhiteProcess.Sigma)
            .WithForwardMeasureTime(forwardMeasureTime);
        hullWhiteModel = new HullWhite(riskFree, hullWhiteProcess.A, hullWhiteProcess.Sigma);
        this.corrEquityShortRate = corrEquityShortRate;
        scheme = discretization;
        maxRho = Math.Sqrt(1 - (hestonProcess.Rho * hestonProcess.Rho))
            - Math.Sqrt(NumericLimits.QL_EPSILON); // reserve for rounding errors
        endDiscount = riskFree.Discount(forwardMeasureTime);

        if (!((corrEquityShortRate * corrEquityShortRate) + (hestonProcess.Rho * hestonProcess.Rho) <= 1.0))
        {
            throw new ArgumentException("correlation matrix is not positive definite", nameof(corrEquityShortRate));
        }

        if (!(hullWhiteProcess.Sigma > 0.0))
        {
            throw new ArgumentException("positive vol of Hull White process is required", nameof(hullWhiteProcess));
        }
    }

    /// <inheritdoc/>
    public override int Size => 3;

    /// <summary>Gets the Heston process.</summary>
    public HestonProcess HestonProcess => hestonProcess;

    /// <summary>Gets the forward Hull-White process the hybrid evolves: on the Heston process's risk-free curve, with
    /// the given process's reversion, volatility and forward-measure time.</summary>
    public HullWhiteForwardProcess HullWhiteProcess => hullWhiteProcess;

    /// <summary>Gets the correlation of the equity with the short rate: QuantLib's <c>eta()</c>.</summary>
    public double Eta => corrEquityShortRate;

    /// <summary>Gets the discretization.</summary>
    public HybridHestonHullWhiteProcessDiscretization Discretization => scheme;

    /// <inheritdoc/>
    public override Vector InitialValues() => new([hestonProcess.S0, hestonProcess.V0, hullWhiteProcess.X0]);

    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="result"/> is shorter than the process's size.</exception>
    public override void InitialValues(Span<double> result)
    {
        CheckResult(result.Length);
        result[0] = hestonProcess.S0;
        result[1] = hestonProcess.V0;
        result[2] = hullWhiteProcess.X0;
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">
    /// <paramref name="x"/> holds fewer values than the state, where QuantLib reads past its end.
    /// </exception>
    public override Vector Drift(double t, Vector x)
    {
        ArgumentNullException.ThrowIfNull(x);
        CheckState(x.Size, nameof(x));
        Vector y0 = hestonOnClock.Drift(t, new Vector([x[0], x[1]]));
        return new Vector([y0[0], y0[1], hullWhiteProcess.Drift(t, x[2])]);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">
    /// <paramref name="x0"/> or <paramref name="dx"/> holds fewer values than the state, where QuantLib reads past its
    /// end.
    /// </exception>
    public override Vector Apply(Vector x0, Vector dx)
    {
        ArgumentNullException.ThrowIfNull(x0);
        ArgumentNullException.ThrowIfNull(dx);
        CheckState(x0.Size, nameof(x0));
        CheckState(dx.Size, nameof(dx));
        Vector yt = hestonOnClock.Apply(new Vector([x0[0], x0[1]]), new Vector([dx[0], dx[1]]));
        return new Vector([yt[0], yt[1], hullWhiteProcess.Apply(x0[2], dx[2])]);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException">
    /// <paramref name="x"/> holds fewer values than the Heston process's state, where QuantLib reads past its end.
    /// </exception>
    public override Matrix Diffusion(double t, Vector x)
    {
        ArgumentNullException.ThrowIfNull(x);
        if (!(x.Size >= 2))
        {
            throw new ArgumentException($"{x.Size} values given for the Heston process's state of 2", nameof(x));
        }

        var retVal = new Matrix(3, 3);

        Matrix m = hestonOnClock.Diffusion(t, new Vector([x[0], x[1]]));
        retVal[0, 0] = m[0, 0];
        retVal[0, 1] = 0.0;
        retVal[0, 2] = 0.0;
        retVal[1, 0] = m[1, 0];
        retVal[1, 1] = m[1, 1];
        retVal[1, 2] = 0.0;

        double sigma = hullWhiteProcess.Sigma;
        retVal[2, 0] = corrEquityShortRate * sigma;
        retVal[2, 1] = -retVal[2, 0] * retVal[1, 0] / retVal[1, 1];
        retVal[2, 2] = Math.Sqrt((sigma * sigma) - (retVal[2, 1] * retVal[2, 1]) - (retVal[2, 0] * retVal[2, 0]));

        return retVal;
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The step's variances aren't positive ("zero or negative variance given"), or
    /// its terminal correlation is invalid.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="x0"/> holds fewer values than the state, or <paramref name="dw"/> fewer increments than the
    /// process's factors, where QuantLib reads past its end.
    /// </exception>
    public override Vector Evolve(double t0, Vector x0, double dt, Vector dw)
    {
        ArgumentNullException.ThrowIfNull(x0);
        ArgumentNullException.ThrowIfNull(dw);
        CheckState(x0.Size, nameof(x0));
        CheckIncrements(dw.Size, nameof(dw));
        var retVal = new Vector(3);
        Step(t0, x0.AsSpan(), dt, dw.AsSpan(), retVal.AsSpan(), false);
        return retVal;
    }

    /// <inheritdoc/>
    /// <remarks>The same arithmetic without allocating, so the values are
    /// <see cref="Evolve(double, Vector, double, Vector)"/>'s bit for bit.</remarks>
    /// <exception cref="NanoQLException">The step's variances aren't positive, or its terminal correlation is
    /// invalid.</exception>
    /// <exception cref="ArgumentException">
    /// <paramref name="x0"/> holds fewer values than the state, or <paramref name="dw"/> fewer increments than the
    /// process's factors, where QuantLib reads past their end; or <paramref name="result"/> is shorter than the state.
    /// </exception>
    public override void Evolve(double t0, ReadOnlySpan<double> x0, double dt, ReadOnlySpan<double> dw, Span<double> result)
    {
        CheckState(x0.Length, nameof(x0));
        CheckIncrements(dw.Length, nameof(dw));
        CheckResult(result.Length);
        Step(t0, x0, dt, dw, result, true);
    }

    /// <summary>The numeraire at a time and state: the Hull-White zero bond to the forward-measure time over its
    /// initial discount.</summary>
    /// <param name="t">The time.</param>
    /// <param name="x">The state.</param>
    /// <returns>The numeraire's value.</returns>
    /// <exception cref="ArgumentException">
    /// <paramref name="x"/> holds fewer values than the state, where QuantLib reads past its end.
    /// </exception>
    public double Numeraire(double t, Vector x)
    {
        ArgumentNullException.ThrowIfNull(x);
        CheckState(x.Size, nameof(x));
        return NumeraireAt(t, x[2]);
    }

    // the numeraire at a time and short rate, which is all of the state it reads, for the allocation-free path pricer
    internal double NumeraireAt(double t, double rate) => hullWhiteModel.DiscountBond(t, forwardMeasureTime, rate) / endDiscount;

    /// <summary>The time of a date: on the Heston process's model clock, QuantLib's, the risk-free curve's, where the
    /// dividend curve isn't dated before it.</summary>
    /// <param name="date">The date.</param>
    /// <returns>The time.</returns>
    /// <exception cref="ArgumentException"><paramref name="date"/> is <c>default(Date)</c>.</exception>
    public override double Time(Date date)
    {
        Date.ThrowIfDefault(date, nameof(date));
        return clock.TimeFromReference(date);
    }

    // QuantLib's evolve (hybridhestonhullwhiteprocess.cpp:107-197) on the model's clock; inPlace reads the rates
    // without building QuantLib's rate objects
    private void Step(double t0, ReadOnlySpan<double> x0, double dt, ReadOnlySpan<double> dw, Span<double> retVal, bool inPlace)
    {
        double r = x0[2];
        double a = hullWhiteProcess.A;
        double sigma = hullWhiteProcess.Sigma;
        double rho = corrEquityShortRate;
        double xi = hestonProcess.Rho;
        double eta = x0[1] > 0.0 ? Math.Sqrt(x0[1]) : 0.0;
        double s = t0;
        double t = t0 + dt;
        double tMeasure = forwardMeasureTime;
        double dy = DividendRate(s, t, inPlace);

        double df = Math.Log(riskFree.Discount(t) / riskFree.Discount(s));

        double m1 = (-(dy + (0.5 * eta * eta)) * dt) - df;
        double m2 = Term(StepTerm.M2, a, sigma, eta, rho, s, t, dt, tMeasure);
        double m3 = (r - hullWhiteProcess.Alpha(s)) * hullWhiteProcess.B(s, t);
        double m4 = Term(StepTerm.M4, a, sigma, eta, rho, s, t, dt, tMeasure);
        double m5 = Term(StepTerm.M5, a, sigma, eta, rho, s, t, dt, tMeasure);
        double mu = m1 + m2 + m3 + m4 + m5;

        double eta2 = hestonProcess.Sigma * eta;
        double nu = hestonProcess.Kappa * (hestonProcess.Theta - (eta * eta));

        double v = x0[1] + (nu * dt) + (eta2 * Math.Sqrt(dt) * ((xi * dw[0]) + (Math.Sqrt(1 - (xi * xi)) * dw[1])));

        if (scheme == HybridHestonHullWhiteProcessDiscretization.BSMHullWhite)
        {
            double v1 = Term(StepTerm.V1, a, sigma, eta, rho, s, t, dt, tMeasure);
            double v2 = hullWhiteProcess.Variance(t0, r, dt);
            double v12 = Term(StepTerm.V12, a, sigma, eta, rho, s, t, dt, tMeasure);

            if (!(v1 > 0.0 && v2 > 0.0))
            {
                throw new NanoQLException("zero or negative variance given");
            }

            // terminal correlation
            double rhoT = Math.Min(maxRho, Math.Max(-maxRho, v12 / Math.Sqrt(v1 * v2)));
            if (!(rhoT <= 1.0 && rhoT >= -1.0 && 1 - (rhoT * rhoT / (1 - (xi * xi))) >= 0.0))
            {
                throw new NanoQLException("invalid terminal correlation");
            }

            double dw0 = dw[0];
            double dw2 = (rhoT * dw[0]) - (rhoT * xi / Math.Sqrt(1 - (xi * xi)) * dw[1])
                + (Math.Sqrt(1 - (rhoT * rhoT / (1 - (xi * xi)))) * dw[2]);

            double rate = hullWhiteProcess.Evolve(t0, r, dt, dw2);

            double vol = Math.Sqrt(v1) * dw0;
            retVal[0] = x0[0] * Math.Exp(mu + vol);
            retVal[1] = v;
            retVal[2] = rate;
        }
        else
        {
            double dw2 = (rho * dw[0]) - (rho * xi / Math.Sqrt(1 - (xi * xi)) * dw[1])
                + (Math.Sqrt(1 - (rho * rho / (1 - (xi * xi)))) * dw[2]);

            double rate = hullWhiteProcess.Evolve(t0, r, dt, dw2);

            double vol = eta * Math.Sqrt(dt) * dw[0];
            retVal[0] = x0[0] * Math.Exp(mu + vol);
            retVal[1] = v;
            retVal[2] = rate;
        }
    }

    // q over [s, t] on the model's clock: QuantLib's read where the dividend curve shares the clock's times, otherwise
    // at the instants they denote; in place without QuantLib's rate objects, the same arithmetic
    private double DividendRate(double s, double t, bool inPlace) =>
        dividends.SharesTimes(clock)
            ? (inPlace ? dividends.ContinuousForwardRate(s, t) : dividends.ForwardRate(s, t, Compounding.Continuous, Frequency.NoFrequency).Rate)
            : dividends.ContinuousForwardRateAt(clock, s, t);

    // one of the step's terms, in the arithmetic a·dt calls for
    private static double Term(StepTerm term, double a, double sigma, double eta, double rho, double s, double t, double dt, double tMeasure) =>
        IKappaTExpression.Evaluate(
            new StepTermExpression(term, sigma, eta, rho, s, t, dt, tMeasure), a, dt, StepCut, StepFloor, StepQuadFloor, double.PositiveInfinity);

    // QuantLib's step terms (hybridhestonhullwhiteprocess.cpp:127-165), its powers of e^{-a·} computed as it computes
    // them; in the wider arithmetics the step's length is t - s, exact, where QuantLib's dt may differ from it by a
    // rounding, which the terms divide by a² and a³
    internal static T StepTermValue<T>(StepTerm term, T a, double sigma, double eta, double rho, double s, double t, double dt, double tMeasure)
        where T : struct, IRealArithmetic<T>
    {
        T eaT = T.Exp(-a * tMeasure);
        T eat = T.Exp(-a * t);
        T eas = T.Exp(-a * s);
        T iat = 1.0 / eat;
        T ias = 1.0 / eas;
        T dtT = typeof(T) == typeof(RealDouble) ? T.Of(dt) : T.Of(t) - T.Of(s);
        return term switch
        {
            StepTerm.M2 => -rho * sigma * eta / a * (dtT - (1 / a * eaT * (iat - ias))),
            StepTerm.M4 => sigma * sigma / (2 * a * a) * (dtT + (2 / a * (eat - eas)) - (1 / (2 * a) * ((eat * eat) - (eas * eas)))),
            StepTerm.M5 => -sigma * sigma / (a * a)
                * (dtT - (1 / a * (1 - (eat * ias))) - (1 / (2 * a) * eaT * (iat - (2 * ias) + (eat * ias * ias)))),
            StepTerm.V1 => (eta * eta * dtT)
                + (sigma * sigma / (a * a) * (dtT - (2 / a * (1 - (eat * ias))) + (1 / (2 * a) * (1 - (eat * eat * ias * ias)))))
                + (2 * sigma * eta / a * rho * (dtT - (1 / a * (1 - (eat * ias))))),
            _ => ((1 - (eat * ias)) * ((sigma * eta / a * rho) + (sigma * sigma / (a * a))))
                - (sigma * sigma / (2 * a * a) * (1 - (eat * eat * ias * ias))),
        };
    }

    // the step's terms
    internal enum StepTerm
    {
        M2,
        M4,
        M5,
        V1,
        V12,
    }

    // one step term as a function of a, its other inputs fixed
    private readonly struct StepTermExpression(
        StepTerm term, double sigma, double eta, double rho, double s, double t, double dt, double tMeasure) : IKappaTExpression
    {
        public T Evaluate<T>(T a)
            where T : struct, IRealArithmetic<T> =>
            StepTermValue(term, a, sigma, eta, rho, s, t, dt, tMeasure);
    }
}
