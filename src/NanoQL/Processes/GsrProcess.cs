// SPDX-FileCopyrightText: Copyright (C) 2013, 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/processes/gsrprocess.hpp ql/processes/gsrprocess.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Time;

namespace NanoQL.Processes;

/// <summary>
/// The GSR model's state process, with piecewise constant volatilities and reversions, its dynamics expressed in the
/// forward measure of a time T. For a derivation of the formulas, see http://ssrn.com/abstract=2246013.
/// </summary>
/// <remarks>
/// <para>
/// Immutable. QuantLib's process caches its integrals for performance, so that a parameter change needs
/// <c>flushCache()</c>; here each public member computes its terms afresh, within one call, and a model's engine call
/// shares one memo across its reads through the internal overloads. QuantLib's <c>flushCache</c> and its parameter
/// setters, which only the model calls, have no counterpart: a parameter change builds a new process.
/// </para>
/// <para>
/// <see cref="WithForwardMeasureTime"/> moves the core's forward measure time with the process's and refuses one below
/// the last step time, as QuantLib's master does after the pin (#2842): QuantLib 1.44's leaves its core at the
/// constructor's time.
/// </para>
/// </remarks>
public sealed class GsrProcess : ForwardMeasureProcess1D
{
    private readonly GsrProcessCore core;
    private readonly Date? referenceDate;
    private readonly DayCounter? dc;

    /// <summary>Initializes a new instance of the <see cref="GsrProcess"/> class.</summary>
    /// <param name="times">The step times, increasing.</param>
    /// <param name="vols">The volatilities: one more than the step times.</param>
    /// <param name="reversions">The reversions: one, constant, or one more than the step times.</param>
    /// <param name="T">The forward measure time; 60 by default, QuantLib's. It may lie below the last step time, as
    /// QuantLib accepts it: a step past it moves nothing, and nothing past it is read.</param>
    /// <param name="referenceDate">The reference date of <see cref="Time"/>; <see langword="null"/>, QuantLib's empty
    /// date, by default.</param>
    /// <param name="dc">The day counter of <see cref="Time"/>; <see langword="null"/>, QuantLib's empty day counter, by
    /// default.</param>
    /// <exception cref="ArgumentNullException"><paramref name="times"/>, <paramref name="vols"/> or
    /// <paramref name="reversions"/> is null.</exception>
    /// <exception cref="ArgumentException">The counts don't match, or the times aren't increasing.</exception>
    public GsrProcess(
        IEnumerable<double> times,
        IEnumerable<double> vols,
        IEnumerable<double> reversions,
        double T = 60.0,
        Date? referenceDate = null,
        DayCounter? dc = null)
        : this(new GsrProcessCore(Copied(times), Copied(vols), Copied(reversions), T), referenceDate, dc)
    {
    }

    // A process over a built core, at its forward measure time.
    internal GsrProcess(GsrProcessCore core, Date? referenceDate, DayCounter? dc)
        : base(core.ForwardMeasureTime)
    {
        this.core = core;
        this.referenceDate = referenceDate;
        this.dc = dc;
    }

    /// <inheritdoc/>
    /// <remarks>0: the state starts at 0.</remarks>
    public override double X0 => 0.0;

    // The core, for the model's members.
    internal GsrProcessCore Core => core;

    /// <inheritdoc/>
    /// <remarks>y(t) − G(t, T)·σ(t)² − a(t)·x, as QuantLib's, which doesn't check t against T.</remarks>
    public override double Drift(double t, double x) => Drift(t, x, new GsrProcessCoreMemo());

    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="t"/> is negative or past the forward measure
    /// time.</exception>
    public override double Diffusion(double t, double x)
    {
        CheckT(t, nameof(t));
        return Sigma(t);
    }

    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="t0"/> + <paramref name="dt"/> is negative or past the
    /// forward measure time.</exception>
    public override double Expectation(double t0, double x0, double dt) => Expectation(t0, x0, dt, new GsrProcessCoreMemo());

    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="t0"/> + <paramref name="dt"/> is negative or past the
    /// forward measure time.</exception>
    public override double StdDeviation(double t0, double x0, double dt) => StdDeviation(t0, x0, dt, new GsrProcessCoreMemo());

    /// <inheritdoc/>
    /// <exception cref="ArgumentException"><paramref name="t0"/> + <paramref name="dt"/> is negative or past the
    /// forward measure time.</exception>
    public override double Variance(double t0, double x0, double dt) => Variance(t0, x0, dt, new GsrProcessCoreMemo());

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The process has no reference date or no day counter.</exception>
    public override double Time(Date date)
    {
        if (!(referenceDate is not null && dc is not null))
        {
            throw new NanoQLException("time can not be computed without reference date and day counter");
        }

        return dc.YearFraction(referenceDate.Value, date);
    }

    /// <summary>
    /// A copy of the process at another forward measure time: QuantLib's <c>setForwardMeasureTime</c>, its core moved
    /// with it and the time checked against the step times, as QuantLib's master does after the pin (#2842).
    /// </summary>
    /// <param name="forwardMeasureTime">The forward measure time T.</param>
    /// <returns>The copy; the receiver doesn't change.</returns>
    /// <exception cref="ArgumentException"><paramref name="forwardMeasureTime"/> is below the last step
    /// time.</exception>
    public override GsrProcess WithForwardMeasureTime(double forwardMeasureTime) =>
        new(core.WithForwardMeasureTime(forwardMeasureTime, nameof(forwardMeasureTime)), referenceDate, dc);

    /// <summary>The volatility at a time.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The volatility of the piece the time falls in.</returns>
    public double Sigma(double t) => core.Sigma(t);

    /// <summary>The reversion at a time.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The reversion of the piece the time falls in.</returns>
    public double Reversion(double t) => core.Reversion(t);

    /// <summary>y(t), the variance of the state at t in the risk-neutral measure.</summary>
    /// <param name="t">The time.</param>
    /// <returns>y(t).</returns>
    /// <exception cref="ArgumentException"><paramref name="t"/> is negative or past the forward measure
    /// time.</exception>
    public double Y(double t) => Y(t, new GsrProcessCoreMemo());

    /// <summary>G(t, T): the integral of the state's decay from t to T.</summary>
    /// <param name="t">The start time.</param>
    /// <param name="T">The end time, at most the forward measure time.</param>
    /// <param name="x">The state, which the result doesn't depend on.</param>
    /// <returns>G(t, T).</returns>
    /// <exception cref="ArgumentException"><paramref name="T"/> is below <paramref name="t"/>, or the two aren't in [0,
    /// T].</exception>
    public double G(double t, double T, double x) => G(t, T, new GsrProcessCoreMemo());

    // The members over a call's memo, which the model's internal members share within one call.
    internal double Drift(double t, double x, GsrProcessCoreMemo memo) =>
        core.Y(t, memo) - (core.G(t, ForwardMeasureTime, memo) * Sigma(t) * Sigma(t)) - (Reversion(t) * x);

    internal double Expectation(double w, double xw, double dt, GsrProcessCoreMemo memo)
    {
        CheckT(w + dt, nameof(dt));
        return core.ExpectationX0DepPart(w, xw, dt, memo) + core.ExpectationRnPart(w, dt, memo) + core.ExpectationTfPart(w, dt, memo);
    }

    internal double StdDeviation(double t0, double x0, double dt, GsrProcessCoreMemo memo) => Math.Sqrt(Variance(t0, x0, dt, memo));

    internal double Variance(double w, double x, double dt, GsrProcessCoreMemo memo)
    {
        CheckT(w + dt, nameof(dt));
        return core.Variance(w, dt, memo);
    }

    internal double Y(double t, GsrProcessCoreMemo memo)
    {
        CheckT(t, nameof(t));
        return core.Y(t, memo);
    }

    internal double G(double t, double w, GsrProcessCoreMemo memo)
    {
        if (!(w >= t))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"G(t,w) should be called with w ({w}) not lesser than t ({t})"),
                "T");
        }

        if (!(t >= 0.0 && w <= ForwardMeasureTime))
        {
            throw new ArgumentException(
                string.Create(CultureInfo.InvariantCulture, $"G(t,w) should be called with (t,w)=({t},{w}) in Range [0,{ForwardMeasureTime}]."),
                "T");
        }

        return core.G(t, w, memo);
    }

    private static ImmutableArray<double> Copied(IEnumerable<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        return [.. values];
    }

    private void CheckT(double t, string paramName)
    {
        if (!(t <= ForwardMeasureTime && t >= 0.0))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"t ({t}) must not be greater than forward measure time ({ForwardMeasureTime}) and non-negative"),
                paramName);
        }
    }
}
