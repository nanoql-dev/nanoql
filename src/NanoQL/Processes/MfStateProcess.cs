// SPDX-FileCopyrightText: Copyright (C) 2013 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/processes/mfstateprocess.hpp ql/processes/mfstateprocess.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Numerics;

namespace NanoQL.Processes;

/// <summary>
/// The Markov-functional model's state process, dx = σ(t) e^{at} dW(t), with a piecewise constant volatility σ and a
/// constant reversion a.
/// </summary>
/// <remarks>
/// <para>
/// Immutable: the model's calibration, which QuantLib's changes the process's volatilities for, builds a new process.
/// A reversion below QuantLib's epsilon in size is taken as 0.
/// </para>
/// <para>
/// Three of QuantLib's defects are fixed. The diffusion is σ(t) e^{at}, its equation's, where QuantLib's omits the
/// factor e^{at} (<c>mfstateprocess.cpp:62-66</c>). The variance with no step times is the volatility's square times
/// the integral of e^{2as}, where QuantLib's leaves out the square (<c>:80-84</c>), the stepped variance's form with one
/// step past the horizon. And below a reversion of 1e-2 in size each step's variance is
/// σ² e^{2as}·expm1(2a(u − s))/(2a) over [s, u], continuous in a, where QuantLib's (e^{2au} − e^{2as})/(2a) cancels as
/// a falls; at and above 1e-2, QuantLib's form and bits.
/// </para>
/// </remarks>
public sealed class MfStateProcess : StochasticProcess1D
{
    private readonly double reversion;
    private readonly bool reversionZero;
    private readonly ImmutableArray<double> times;
    private readonly ImmutableArray<double> vols;

    /// <summary>Initializes a new instance of the <see cref="MfStateProcess"/> class.</summary>
    /// <param name="reversion">The reversion.</param>
    /// <param name="times">The step times, increasing.</param>
    /// <param name="vols">The volatilities, non negative: one more than the step times.</param>
    /// <exception cref="ArgumentNullException"><paramref name="times"/> or <paramref name="vols"/> is null.</exception>
    /// <exception cref="ArgumentException">The counts don't match, the times aren't increasing, or a volatility is
    /// negative.</exception>
    public MfStateProcess(double reversion, IEnumerable<double> times, IEnumerable<double> vols)
        : this(reversion, Copied(times), Copied(vols))
    {
    }

    private MfStateProcess(double reversion, ImmutableArray<double> times, ImmutableArray<double> vols)
    {
        this.reversion = reversion;
        reversionZero = reversion < NumericLimits.QL_EPSILON && -reversion < NumericLimits.QL_EPSILON;
        this.times = times;
        this.vols = vols;
        CheckTimesVols(times, vols);
    }

    /// <summary>Gets 0, the state's initial value.</summary>
    public override double X0 => 0.0;

    /// <summary>Gets the step times.</summary>
    internal ImmutableArray<double> Times => times;

    /// <summary>The drift: 0.</summary>
    /// <param name="t">The time.</param>
    /// <param name="x">The state.</param>
    /// <returns>0.</returns>
    public override double Drift(double t, double x) => 0.0;

    /// <summary>The diffusion σ(t) e^{at}.</summary>
    /// <param name="t">The time.</param>
    /// <param name="x">The state.</param>
    /// <returns>The diffusion.</returns>
    public override double Diffusion(double t, double x)
    {
        int i = Interpolation.UpperBound(times.AsSpan(), t);
        return vols[i] * Math.Exp(reversion * t);
    }

    /// <summary>The expectation: the state at the interval's start.</summary>
    /// <param name="t0">The start of the interval.</param>
    /// <param name="x0">The state at its start.</param>
    /// <param name="dt">The interval's length.</param>
    /// <returns><paramref name="x0"/>.</returns>
    public override double Expectation(double t0, double x0, double dt) => x0;

    /// <summary>The standard deviation over an interval: the square root of <see cref="Variance"/>.</summary>
    /// <param name="t0">The start of the interval.</param>
    /// <param name="x0">The state at its start.</param>
    /// <param name="dt">The interval's length.</param>
    /// <returns>The standard deviation.</returns>
    public override double StdDeviation(double t0, double x0, double dt) => Math.Sqrt(Variance(t0, x0, dt));

    /// <summary>The variance over an interval, the integral of σ(s)² e^{2as} over it.</summary>
    /// <param name="t0">The start of the interval.</param>
    /// <param name="x0">The state at its start.</param>
    /// <param name="dt">The interval's length.</param>
    /// <returns>The variance; 0 for an interval shorter than QuantLib's epsilon.</returns>
    public override double Variance(double t0, double x0, double dt)
    {
        double t = t0;
        if (dt < NumericLimits.QL_EPSILON)
        {
            return 0.0;
        }

        if (times.IsEmpty)
        {
            return Step(vols[0], t, dt, t + dt);
        }

        ReadOnlySpan<double> ts = times.AsSpan();
        int i = Interpolation.UpperBound(ts, t);
        int j = Interpolation.UpperBound(ts, t + dt);

        double v = 0.0;

        for (int k = i; k < j; k++)
        {
            double start = Max(k > 0 ? ts[k - 1] : 0.0, t);
            v += Step(vols[k], start, ts[k] - start, ts[k]);
        }

        double last = Max(j > 0 ? ts[j - 1] : 0.0, t);
        v += Step(vols[j], last, t + dt - last, t + dt);

        return v;
    }

    // A copy with other volatilities, as QuantLib's setVols.
    internal MfStateProcess WithVols(ImmutableArray<double> vols) => new(reversion, times, vols);

    // A copy with other times and volatilities, as QuantLib's setTimes and setVols.
    internal MfStateProcess WithTimesVols(ImmutableArray<double> times, ImmutableArray<double> vols) =>
        new(reversion, times, vols);

    private static ImmutableArray<double> Copied(IEnumerable<double> values, [System.Runtime.CompilerServices.CallerArgumentExpression(nameof(values))] string? paramName = null)
    {
        ArgumentNullException.ThrowIfNull(values, paramName);
        return [.. values];
    }

    private static void CheckTimesVols(ImmutableArray<double> times, ImmutableArray<double> vols)
    {
        if (!(times.Length == vols.Length - 1))
        {
            throw new ArgumentException(
                $"number of volatilities ({vols.Length}) compared to number of times ({times.Length} must be bigger by one",
                nameof(vols));
        }

        for (int i = 0; i < times.Length - 1; i++)
        {
            if (!(times[i] < times[i + 1]))
            {
                throw new ArgumentException(
                    $"times must be increasing ({Show(times[i])}@{i} , {Show(times[i + 1])}@{i + 1})", nameof(times));
            }
        }

        for (int i = 0; i < vols.Length; i++)
        {
            if (!(vols[i] >= 0.0))
            {
                throw new ArgumentException($"volatilities must be non negative ({Show(vols[i])}@{i})", nameof(vols));
            }
        }
    }

    // std::max(a, b), which keeps a where b is NaN.
    private static double Max(double a, double b) => a < b ? b : a;

    private static string Show(double x) => x.ToString(CultureInfo.InvariantCulture);

    // A step's variance, σ² ∫ e^{2as} ds over [start, end], of the given length: QuantLib's form at a = 0 and from
    // 1e-2, the expm1 form between (the S-g1d plan's G1R4 (ii)).
    private double Step(double vol, double start, double length, double end)
    {
        if (reversionZero)
        {
            return vol * vol * length;
        }

        if (Math.Abs(reversion) < GsrProcessCore.SmallReversion)
        {
            double a2 = 2.0 * reversion;
            return vol * vol * (Math.Exp(a2 * start) * Expm1Functions.StdExpm1(a2 * length) / a2);
        }

        return 1.0 / (2.0 * reversion) * vol * vol * (Math.Exp(2.0 * reversion * end) - Math.Exp(2.0 * reversion * start));
    }
}
