// SPDX-FileCopyrightText: Copyright (C) 2015 Peter Caspers
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/processes/gsrprocesscore.hpp ql/processes/gsrprocesscore.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using NanoQL.Numerics;

namespace NanoQL.Processes;

/// <summary>
/// The core computations of the GSR process, in the risk-neutral and the T-forward measure: QuantLib's
/// <c>detail::GsrProcessCore</c>, for piecewise constant volatilities and reversions on the step times.
/// </summary>
/// <remarks>
/// <para>
/// Immutable. QuantLib's core caches its integrals in six maps that every parameter change flushes; here each member
/// takes the call's <see cref="GsrProcessCoreMemo"/>, which holds them for one call only, and a change of parameters,
/// times or forward measure time builds a new core (the S-g1d plan's G1R1 (iii)).
/// </para>
/// <para>
/// Below a reversion of 1e-2 in absolute value, each term is computed in a form continuous in the reversion: the
/// first-order kernels (1 − e^(−ad))/a through <c>expm1</c>, and the two terms over a², the risk-neutral part's
/// αₖζₖ and the forward-measure part's ηₖζₖ, through φ₂(u) = (eᵘ − 1 − u)/u²; at a reversion of exactly 0, QuantLib's
/// zero-reversion formulas, the forms' limits; at and above 1e-2, QuantLib's formulas and bits. QuantLib switches to
/// its zero-reversion formulas below 1e-4, so its outputs jump there, and above it its terms over a² cancel to second
/// order: a documented divergence (the plan's G1R4 (i); <c>Divergences/GsrSmallReversionTests.cs</c>).
/// </para>
/// <para>
/// <see cref="WithForwardMeasureTime"/> moves the forward measure time and refuses one below the last step time, as
/// QuantLib's master does after the pin (#2842); QuantLib 1.44's core keeps its constructor's time.
/// </para>
/// </remarks>
internal sealed class GsrProcessCore
{
    // The reversion below which the cancellation-free forms replace QuantLib's (the plan's G1R4 (i)).
    internal const double SmallReversion = 1e-2;

    private readonly ImmutableArray<double> times;
    private readonly ImmutableArray<double> vols;
    private readonly ImmutableArray<double> reversions;
    private readonly double forwardMeasureTime;

    // QuantLib's revZero_, a reversion of exactly 0 here; and the reversions taking the cancellation-free forms.
    private readonly ImmutableArray<bool> revZero;
    private readonly ImmutableArray<bool> revSmall;

    /// <summary>Initializes a new instance of the <see cref="GsrProcessCore"/> class.</summary>
    /// <param name="times">The step times, increasing.</param>
    /// <param name="vols">The volatilities, one more than the step times.</param>
    /// <param name="reversions">The reversions: one, or one more than the step times.</param>
    /// <param name="T">The forward measure time.</param>
    /// <exception cref="ArgumentException">The counts don't match, or the times aren't increasing.</exception>
    public GsrProcessCore(ImmutableArray<double> times, ImmutableArray<double> vols, ImmutableArray<double> reversions, double T)
    {
        this.times = times;
        this.vols = vols;
        this.reversions = reversions;
        forwardMeasureTime = T;
        var zero = new bool[reversions.Length];
        var small = new bool[reversions.Length];
        for (int i = 0; i < reversions.Length; i++)
        {
            zero[i] = reversions[i] == 0.0;
            small[i] = !zero[i] && Math.Abs(reversions[i]) < SmallReversion;
        }

        revZero = [.. zero];
        revSmall = [.. small];
        CheckTimesVolsReversions(times, vols, reversions);
    }

    /// <summary>Gets the forward measure time T.</summary>
    public double ForwardMeasureTime => forwardMeasureTime;

    /// <summary>Gets the step times.</summary>
    public ImmutableArray<double> Times => times;

    /// <summary>Gets the volatilities.</summary>
    public ImmutableArray<double> Vols => vols;

    /// <summary>Gets the reversions.</summary>
    public ImmutableArray<double> Reversions => reversions;

    /// <summary>
    /// A copy of the core at another forward measure time: QuantLib's master's <c>setForwardMeasureTime</c> (#2842).
    /// </summary>
    /// <param name="T">The forward measure time.</param>
    /// <param name="paramName">The caller's parameter name, for the refusal.</param>
    /// <returns>The copy.</returns>
    /// <exception cref="ArgumentException"><paramref name="T"/> is below the last step time.</exception>
    public GsrProcessCore WithForwardMeasureTime(double T, string paramName)
    {
        CheckForwardMeasureTime(times, T, paramName);
        return new GsrProcessCore(times, vols, reversions, T);
    }

    /// <summary>A copy of the core with other volatilities and reversions, its times and forward measure time
    /// kept.</summary>
    /// <param name="newVols">The volatilities.</param>
    /// <param name="newReversions">The reversions.</param>
    /// <returns>The copy.</returns>
    public GsrProcessCore WithParameters(ImmutableArray<double> newVols, ImmutableArray<double> newReversions) =>
        new(times, newVols, newReversions, forwardMeasureTime);

    // QuantLib's master's check in setForwardMeasureTime (#2842), with its text.
    internal static void CheckForwardMeasureTime(ImmutableArray<double> times, double T, string paramName)
    {
        if (!(times.IsEmpty || T >= times[^1]))
        {
            throw new ArgumentException(
                string.Create(
                    CultureInfo.InvariantCulture,
                    $"forward measure time ({T}) must be greater than or equal to the last vol/reversion pillar time ({times[^1]})"),
                paramName);
        }
    }

    /// <summary>The conditional expectation's part that depends on x(w): A(w, t)·x(w).</summary>
    /// <param name="w">The start time.</param>
    /// <param name="xw">The state at <paramref name="w"/>.</param>
    /// <param name="dt">The time step.</param>
    /// <param name="memo">The call's memo.</param>
    /// <returns>The part.</returns>
    public double ExpectationX0DepPart(double w, double xw, double dt, GsrProcessCoreMemo memo)
    {
        double t = w + dt;
        (double, double) key = (w, t);
        if (memo.X0Dependent.TryGetValue(key, out double cached))
        {
            return xw * cached;
        }

        // A(w,t)x(w)
        double res2 = 1.0;
        for (int i = LowerIndex(w); i <= UpperIndex(t) - 1; i++)
        {
            res2 *= Math.Exp(-Rev(i) * (CappedTime(i + 1, t) - FlooredTime(i, w)));
        }

        memo.X0Dependent.TryAdd(key, res2);
        return res2 * xw;
    }

    /// <summary>The conditional expectation's part independent of x(w), in the risk-neutral measure.</summary>
    /// <param name="w">The start time.</param>
    /// <param name="dt">The time step.</param>
    /// <param name="memo">The call's memo.</param>
    /// <returns>The part.</returns>
    public double ExpectationRnPart(double w, double dt, GsrProcessCoreMemo memo)
    {
        double t = w + dt;
        (double, double) key = (w, t);
        if (memo.RiskNeutral.TryGetValue(key, out double cached))
        {
            return cached;
        }

        double res = 0.0;

        // \int A(s,t)y(s)
        for (int k = LowerIndex(w); k <= UpperIndex(t) - 1; k++)
        {
            // l<k
            for (int l = 0; l <= k - 1; l++)
            {
                double res2 = 1.0;

                // alpha_l
                res2 *= RevZero(l) ? Vol(l) * Vol(l) * (Time2(l + 1) - Time2(l))
                    : RevSmall(l) ? Vol(l) * Vol(l) * OneMinusExpOver(2.0 * Rev(l), Time2(l + 1) - Time2(l))
                    : Vol(l) * Vol(l) / (2.0 * Rev(l)) * (1.0 - Math.Exp(-2.0 * Rev(l) * (Time2(l + 1) - Time2(l))));

                // zeta_i (i>k)
                for (int i = k + 1; i <= UpperIndex(t) - 1; i++)
                {
                    res2 *= Math.Exp(-Rev(i) * (CappedTime(i + 1, t) - Time2(i)));
                }

                // beta_j (j<k)
                for (int j = l + 1; j <= k - 1; j++)
                {
                    res2 *= Math.Exp(-2.0 * Rev(j) * (Time2(j + 1) - Time2(j)));
                }

                // zeta_k beta_k
                res2 *= RevZero(k)
                    ? 2.0 * Time2(k) - FlooredTime(k, w) - CappedTime(k + 1, t) - (2.0 * (Time2(k) - CappedTime(k + 1, t)))
                    : RevSmall(k)
                    ? ExpDifferenceOver(
                        Rev(k),
                        2.0 * Time2(k) - FlooredTime(k, w) - CappedTime(k + 1, t),
                        2.0 * (Time2(k) - CappedTime(k + 1, t)),
                        Rev(k))
                    : (Math.Exp(Rev(k) * (2.0 * Time2(k) - FlooredTime(k, w) - CappedTime(k + 1, t)))
                        - Math.Exp(2.0 * Rev(k) * (Time2(k) - CappedTime(k + 1, t)))) / Rev(k);

                // add to sum
                res += res2;
            }

            // l=k
            double res2k = 1.0;

            // alpha_k zeta_k
            res2k *= RevZero(k)
                ? Vol(k) * Vol(k) / 4.0
                    * ((4.0 * Math.Pow(CappedTime(k + 1, t) - Time2(k), 2.0))
                        - (Math.Pow(FlooredTime(k, w) - (2.0 * Time2(k)) + CappedTime(k + 1, t), 2.0)
                            + Math.Pow(CappedTime(k + 1, t) - FlooredTime(k, w), 2.0)))
                : RevSmall(k)
                ? RiskNeutralSecondOrder(Vol(k), Rev(k), CappedTime(k + 1, t) - Time2(k), FlooredTime(k, w) - (2.0 * Time2(k)) + CappedTime(k + 1, t), CappedTime(k + 1, t) - FlooredTime(k, w))
                : Vol(k) * Vol(k) / (2.0 * Rev(k) * Rev(k))
                    * (Math.Exp(-2.0 * Rev(k) * (CappedTime(k + 1, t) - Time2(k))) + 1.0
                        - (Math.Exp(-Rev(k) * (FlooredTime(k, w) - (2.0 * Time2(k)) + CappedTime(k + 1, t)))
                            + Math.Exp(-Rev(k) * (CappedTime(k + 1, t) - FlooredTime(k, w)))));

            // zeta_i (i>k)
            for (int i = k + 1; i <= UpperIndex(t) - 1; i++)
            {
                res2k *= Math.Exp(-Rev(i) * (CappedTime(i + 1, t) - Time2(i)));
            }

            // no beta_j in this case ...
            res += res2k;
        }

        memo.RiskNeutral.TryAdd(key, res);
        return res;
    }

    /// <summary>The conditional expectation's drift adjustment for the T-forward measure.</summary>
    /// <param name="w">The start time.</param>
    /// <param name="dt">The time step.</param>
    /// <param name="memo">The call's memo.</param>
    /// <returns>The part.</returns>
    public double ExpectationTfPart(double w, double dt, GsrProcessCoreMemo memo)
    {
        double t = w + dt;
        (double, double) key = (w, t);
        if (memo.ForwardMeasure.TryGetValue(key, out double cached))
        {
            return cached;
        }

        double bigT = forwardMeasureTime;
        double res = 0.0;

        // int -A(s,t) \sigma^2 G(s,T)
        for (int k = LowerIndex(w); k <= UpperIndex(t) - 1; k++)
        {
            double res2 = 0.0;

            // l>k
            for (int l = k + 1; l <= UpperIndex(bigT) - 1; l++)
            {
                double res3 = 1.0;

                // eta_l
                res3 *= RevZero(l) ? CappedTime(l + 1, bigT) - Time2(l)
                    : RevSmall(l) ? OneMinusExpOver(Rev(l), CappedTime(l + 1, bigT) - Time2(l))
                    : (1.0 - Math.Exp(-Rev(l) * (CappedTime(l + 1, bigT) - Time2(l)))) / Rev(l);

                // zeta_i (i>k)
                for (int i = k + 1; i <= UpperIndex(t) - 1; i++)
                {
                    res3 *= Math.Exp(-Rev(i) * (CappedTime(i + 1, t) - Time2(i)));
                }

                // gamma_j (j>k)
                for (int j = k + 1; j <= l - 1; j++)
                {
                    res3 *= Math.Exp(-Rev(j) * (Time2(j + 1) - Time2(j)));
                }

                // zeta_k gamma_k
                res3 *= RevZero(k)
                    ? (CappedTime(k + 1, t) - Time2(k + 1) - ((2.0 * FlooredTime(k, w)) - CappedTime(k + 1, t) - Time2(k + 1))) / 2.0
                    : RevSmall(k)
                    ? ExpDifferenceOver(
                        Rev(k),
                        CappedTime(k + 1, t) - Time2(k + 1),
                        (2.0 * FlooredTime(k, w)) - CappedTime(k + 1, t) - Time2(k + 1),
                        2.0 * Rev(k))
                    : (Math.Exp(Rev(k) * (CappedTime(k + 1, t) - Time2(k + 1)))
                        - Math.Exp(Rev(k) * ((2.0 * FlooredTime(k, w)) - CappedTime(k + 1, t) - Time2(k + 1)))) / (2.0 * Rev(k));

                // add to sum
                res2 += res3;
            }

            // l=k
            double res3k = 1.0;

            // eta_k zeta_k
            res3k *= RevZero(k)
                ? (-Math.Pow(CappedTime(k + 1, t) - CappedTime(k + 1, bigT), 2.0)
                    - (2.0 * Math.Pow(CappedTime(k + 1, t) - FlooredTime(k, w), 2.0))
                    + Math.Pow((2.0 * FlooredTime(k, w)) - CappedTime(k + 1, bigT) - CappedTime(k + 1, t), 2.0)) / 4.0
                : RevSmall(k)
                ? ForwardMeasureSecondOrder(Rev(k), CappedTime(k + 1, t) - CappedTime(k + 1, bigT), CappedTime(k + 1, t) - FlooredTime(k, w), (2.0 * FlooredTime(k, w)) - CappedTime(k + 1, bigT) - CappedTime(k + 1, t))
                : (2.0 - Math.Exp(Rev(k) * (CappedTime(k + 1, t) - CappedTime(k + 1, bigT)))
                    - ((2.0 * Math.Exp(-Rev(k) * (CappedTime(k + 1, t) - FlooredTime(k, w))))
                        - Math.Exp(Rev(k) * ((2.0 * FlooredTime(k, w)) - CappedTime(k + 1, bigT) - CappedTime(k + 1, t))))) / (2.0 * Rev(k) * Rev(k));

            // zeta_i (i>k)
            for (int i = k + 1; i <= UpperIndex(t) - 1; i++)
            {
                res3k *= Math.Exp(-Rev(i) * (CappedTime(i + 1, t) - Time2(i)));
            }

            // no gamma_j in this case ...
            res2 += res3k;

            // add to main accumulator
            res += -Vol(k) * Vol(k) * res2;
        }

        memo.ForwardMeasure.TryAdd(key, res);
        return res;
    }

    /// <summary>The conditional variance of x(t) given x(w).</summary>
    /// <param name="w">The start time.</param>
    /// <param name="dt">The time step.</param>
    /// <param name="memo">The call's memo.</param>
    /// <returns>The variance.</returns>
    public double Variance(double w, double dt, GsrProcessCoreMemo memo)
    {
        double t = w + dt;
        (double, double) key = (w, t);
        if (memo.Variance.TryGetValue(key, out double cached))
        {
            return cached;
        }

        double res = 0.0;
        for (int k = LowerIndex(w); k <= UpperIndex(t) - 1; k++)
        {
            double res2 = Vol(k) * Vol(k);

            // zeta_k^2
            res2 *= RevZero(k) ? -(FlooredTime(k, w) - CappedTime(k + 1, t))
                : RevSmall(k) ? -Expm1Functions.StdExpm1(2.0 * Rev(k) * (FlooredTime(k, w) - CappedTime(k + 1, t))) / (2.0 * Rev(k))
                : (1.0 - Math.Exp(2.0 * Rev(k) * (FlooredTime(k, w) - CappedTime(k + 1, t)))) / (2.0 * Rev(k));

            // zeta_i (i>k)
            for (int i = k + 1; i <= UpperIndex(t) - 1; i++)
            {
                res2 *= Math.Exp(-2.0 * Rev(i) * (CappedTime(i + 1, t) - Time2(i)));
            }

            res += res2;
        }

        memo.Variance.TryAdd(key, res);
        return res;
    }

    /// <summary>y(t), the variance of the state at t in the risk-neutral measure.</summary>
    /// <param name="t">The time.</param>
    /// <param name="memo">The call's memo.</param>
    /// <returns>y(t).</returns>
    public double Y(double t, GsrProcessCoreMemo memo)
    {
        if (memo.Y.TryGetValue(t, out double cached))
        {
            return cached;
        }

        double res = 0.0;
        for (int i = 0; i <= UpperIndex(t) - 1; i++)
        {
            double res2 = 1.0;
            for (int j = i + 1; j <= UpperIndex(t) - 1; j++)
            {
                res2 *= Math.Exp(-2.0 * Rev(j) * (CappedTime(j + 1, t) - Time2(j)));
            }

            res2 *= RevZero(i) ? Vol(i) * Vol(i) * (CappedTime(i + 1, t) - Time2(i))
                : RevSmall(i) ? Vol(i) * Vol(i) * OneMinusExpOver(2.0 * Rev(i), CappedTime(i + 1, t) - Time2(i))
                : Vol(i) * Vol(i) / (2.0 * Rev(i)) * (1.0 - Math.Exp(-2.0 * Rev(i) * (CappedTime(i + 1, t) - Time2(i))));
            res += res2;
        }

        memo.Y.TryAdd(t, res);
        return res;
    }

    /// <summary>G(t, w): the integral of the state's decay from t to w.</summary>
    /// <param name="t">The start time.</param>
    /// <param name="w">The end time.</param>
    /// <param name="memo">The call's memo.</param>
    /// <returns>G(t, w).</returns>
    public double G(double t, double w, GsrProcessCoreMemo memo)
    {
        (double, double) key = (w, t);
        if (memo.G.TryGetValue(key, out double cached))
        {
            return cached;
        }

        double res = 0.0;
        for (int i = LowerIndex(t); i <= UpperIndex(w) - 1; i++)
        {
            double res2 = 1.0;
            for (int j = LowerIndex(t); j <= i - 1; j++)
            {
                res2 *= Math.Exp(-Rev(j) * (Time2(j + 1) - FlooredTime(j, t)));
            }

            res2 *= RevZero(i) ? CappedTime(i + 1, w) - FlooredTime(i, t)
                : RevSmall(i) ? OneMinusExpOver(Rev(i), CappedTime(i + 1, w) - FlooredTime(i, t))
                : (1.0 - Math.Exp(-Rev(i) * (CappedTime(i + 1, w) - FlooredTime(i, t)))) / Rev(i);
            res += res2;
        }

        memo.G.TryAdd(key, res);
        return res;
    }

    /// <summary>The volatility at a time: the piece the time falls in.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The volatility.</returns>
    public double Sigma(double t) => Vol(LowerIndex(t));

    /// <summary>The reversion at a time: the piece the time falls in.</summary>
    /// <param name="t">The time.</param>
    /// <returns>The reversion.</returns>
    public double Reversion(double t) => Rev(LowerIndex(t));

    // φ₂(u) = (eᵘ − 1 − u)/u², by its series below |u| = 0.5, where the difference would cancel.
    internal static double Phi2(double u)
    {
        if (Math.Abs(u) < 0.5)
        {
            double sum = 0.0;
            double term = 0.5;
            for (int n = 0; n < 24; n++)
            {
                sum += term;
                term *= u / (n + 3);
            }

            return sum;
        }

        return (Expm1Functions.StdExpm1(u) - u) / (u * u);
    }

    // (1 − e^(−c·d))/c, through expm1.
    private static double OneMinusExpOver(double c, double d) => -Expm1Functions.StdExpm1(-c * d) / c;

    // (e^(a·p) − e^(a·q))/div, through expm1.
    private static double ExpDifferenceOver(double a, double p, double q, double div) =>
        Math.Exp(a * q) * Expm1Functions.StdExpm1(a * (p - q)) / div;

    // The risk-neutral part's αₖζₖ, σ²/(2a²)·(e^(−2ax) + 1 − e^(−ay) − e^(−az)) with y + z = 2x, through φ₂.
    private static double RiskNeutralSecondOrder(double vol, double a, double x, double y, double z) =>
        vol * vol / 2.0 * ((4.0 * x * x * Phi2(-2.0 * a * x)) - (y * y * Phi2(-a * y)) - (z * z * Phi2(-a * z)));

    // The forward-measure part's ηₖζₖ, (2 − e^(ap) − 2e^(−aq) + e^(ar))/(2a²) with p − 2q − r = 0, through φ₂.
    private static double ForwardMeasureSecondOrder(double a, double p, double q, double r) =>
        ((-p * p * Phi2(a * p)) - (2.0 * q * q * Phi2(-a * q)) + (r * r * Phi2(a * r))) / 2.0;

    private static void CheckTimesVolsReversions(ImmutableArray<double> times, ImmutableArray<double> vols, ImmutableArray<double> reversions)
    {
        if (!(times.Length == vols.Length - 1))
        {
            throw new ArgumentException(
                $"number of volatilities ({vols.Length}) compared to number of times ({times.Length} must be bigger by one",
                nameof(vols));
        }

        if (!(times.Length == reversions.Length - 1 || reversions.Length == 1))
        {
            // QuantLib's text prints the volatilities' count here.
            throw new ArgumentException(
                $"number of reversions ({vols.Length}) compared to number of times ({times.Length} must be bigger by one, or exactly 1 reversion must be given",
                nameof(reversions));
        }

        for (int i = 0; i < times.Length - 1; i++)
        {
            if (!(times[i] < times[i + 1]))
            {
                throw new ArgumentException(
                    string.Create(CultureInfo.InvariantCulture, $"times must be increasing ({times[i]}@{i} , {times[i + 1]}@{i + 1})"),
                    nameof(times));
            }
        }
    }

    // std::upper_bound over the step times: the first index whose time is greater than t.
    private int LowerIndex(double t) => UpperBound(t);

    private int UpperIndex(double t)
    {
        if (t < NumericLimits.QL_MIN_POSITIVE_REAL)
        {
            return 0;
        }

        return UpperBound(t - NumericLimits.QL_EPSILON) + 1;
    }

    private int UpperBound(double t)
    {
        int first = 0;
        int count = times.Length;
        while (count > 0)
        {
            int step = count / 2;
            int it = first + step;
            if (!(t < times[it]))
            {
                first = it + 1;
                count -= step + 1;
            }
            else
            {
                count = step;
            }
        }

        return first;
    }

    private double CappedTime(int index, double cap)
    {
        double t2 = Time2(index);
        return t2 < cap ? t2 : cap;
    }

    private double FlooredTime(int index, double floor)
    {
        double t2 = Time2(index);
        return floor < t2 ? t2 : floor;
    }

    private double Time2(int index)
    {
        if (index == 0)
        {
            return 0.0;
        }

        if (index > times.Length)
        {
            return forwardMeasureTime;
        }

        return times[index - 1];
    }

    private double Vol(int index) => index >= vols.Length ? vols[^1] : vols[index];

    private double Rev(int index) => index >= reversions.Length ? reversions[^1] : reversions[index];

    private bool RevZero(int index) => index >= revZero.Length ? revZero[^1] : revZero[index];

    private bool RevSmall(int index) => index >= revSmall.Length ? revSmall[^1] : revSmall[index];
}
