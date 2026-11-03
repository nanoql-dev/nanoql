// SPDX-FileCopyrightText: Copyright (C) 2026 Yassine Idyiahia
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/methods/finitedifferences/utilities/smilesectionrndcalculator.hpp ql/methods/finitedifferences/utilities/smilesectionrndcalculator.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Collections.Immutable;
using System.Globalization;
using System.Runtime.ExceptionServices;
using NanoQL.Numerics;
using NanoQL.TermStructures;

namespace NanoQL.Methods;

/// <summary>
/// The risk-neutral density of the logarithm of the underlying at a smile section's expiry, by the Breeden-Litzenberger
/// identity: the density and distribution from the section's prices, the inverse from a table of log-moneyness against
/// the normal quantile z = Φ⁻¹(P(S ≤ K)), linear for a lognormal law and well conditioned in the wings.
/// </summary>
/// <remarks>
/// Immutable. The quantile table is built at construction (the playbook's <c>LazyObject</c> row), where QuantLib builds
/// it at the first inverse; where it can't be built, the refusal is recorded and raised from
/// <see cref="InvCdf(double, double)"/>, QuantLib's point, since the density and distribution never need the table. Its
/// argument x is ln(S); its only time is the section's exercise time.
/// </remarks>
public sealed class SmileSectionRNDCalculator : RiskNeutralDensityCalculator
{
    private const double QL_EPSILON = 2.220446049250313E-16;

    private readonly SmileSection smile;
    private readonly int nStrikes;
    private readonly double nStd;
    private readonly double forward;
    private readonly double zMin;
    private readonly double zMax;
    private readonly ImmutableArray<double> z;
    private readonly ImmutableArray<double> logMoneyness;
    private readonly MonotonicCubicNaturalSpline? quantileFn;
    private readonly ExceptionDispatchInfo? initializationFailure;

    /// <summary>Initializes a new instance of the <see cref="SmileSectionRNDCalculator"/> class.</summary>
    /// <param name="smile">The smile section.</param>
    /// <param name="nStrikes">The quantile table's size, at least 4.</param>
    /// <param name="nStd">The table's half-width in normal quantiles, positive; the inverse saturates there, or at the
    /// deepest the smile still prices.</param>
    /// <exception cref="ArgumentNullException"><paramref name="smile"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Fewer than 4 strikes, or a non-positive half-width, with QuantLib's
    /// messages.</exception>
    public SmileSectionRNDCalculator(SmileSection smile, int nStrikes = 200, double nStd = 8.0)
    {
        ArgumentNullException.ThrowIfNull(smile);
        if (!(nStrikes >= 4))
        {
            throw new ArgumentException(Invariant($"at least 4 strikes required, got {nStrikes}"), nameof(nStrikes));
        }

        if (!(nStd > 0.0))
        {
            throw new ArgumentException(Invariant($"nStd must be positive, got {nStd}"), nameof(nStd));
        }

        this.smile = smile;
        this.nStrikes = nStrikes;
        this.nStd = nStd;

        try
        {
            (forward, z, logMoneyness, quantileFn) = Initialize();
            zMin = z[0];
            zMax = z[^1];
        }
        catch (Exception e) when (e is NanoQLException or ArgumentException)
        {
            initializationFailure = ExceptionDispatchInfo.Capture(e);
        }
    }

    /// <summary>The density of ln(S) at the section's exercise time.</summary>
    /// <param name="x">The logarithm of the underlying.</param>
    /// <param name="t">The time, the section's exercise time within QuantLib's <c>close_enough</c>.</param>
    /// <returns>The density.</returns>
    /// <exception cref="ArgumentException">The time isn't the section's, with QuantLib's message.</exception>
    public override double Pdf(double x, double t)
    {
        CheckTime(t);
        double s = Math.Exp(x);

        // density() floors its own gap relative to the strike, which is what this needs walking out over decades, so
        // the default is already right.
        return s * smile.Density(s, 1.0);
    }

    /// <summary>The distribution function of ln(S) at the section's exercise time, each tail from its own option
    /// type.</summary>
    /// <param name="x">The logarithm of the underlying.</param>
    /// <param name="t">The time, the section's exercise time.</param>
    /// <returns>The probability.</returns>
    /// <exception cref="ArgumentException">The time isn't the section's, with QuantLib's message.</exception>
    public override double Cdf(double x, double t)
    {
        CheckTime(t);

        // Take each tail from its own option type, as the quantile grid does. atmLevel() may be absent, and cdf() does
        // not otherwise need it.
        double k = Math.Exp(x);
        double? atm = smile.AtmLevel();
        if (atm is double a && k <= a)
        {
            return TailProbability(smile, k, OptionType.Put);
        }

        return 1.0 - TailProbability(smile, k, OptionType.Call);
    }

    /// <summary>The inverse of the distribution function at the section's exercise time, from the quantile
    /// table.</summary>
    /// <param name="p">The probability, in (0, 1).</param>
    /// <param name="t">The time, the section's exercise time.</param>
    /// <returns>The logarithm of the underlying.</returns>
    /// <exception cref="ArgumentException">The time isn't the section's, or the probability is outside (0, 1), with
    /// QuantLib's messages.</exception>
    /// <exception cref="NanoQLException">The quantile table couldn't be built: QuantLib's refusal, raised here, as
    /// QuantLib raises it.</exception>
    public override double InvCdf(double p, double t)
    {
        CheckTime(t);
        initializationFailure?.Throw();
        if (!(p > 0.0 && p < 1.0))
        {
            throw new ArgumentException(Invariant($"p must be in (0, 1), got {p}"), nameof(p));
        }

        double zp = Math.Clamp(new InverseCumulativeNormal().Evaluate(p), zMin, zMax);
        return Math.Log(forward) + quantileFn!.Evaluate(zp);
    }

    /// <summary>The density of ln(S) at the section's exercise time.</summary>
    /// <param name="x">The logarithm of the underlying.</param>
    /// <returns>The density.</returns>
    public double Pdf(double x) => Pdf(x, smile.ExerciseTime);

    /// <summary>The distribution function of ln(S) at the section's exercise time.</summary>
    /// <param name="x">The logarithm of the underlying.</param>
    /// <returns>The probability.</returns>
    public double Cdf(double x) => Cdf(x, smile.ExerciseTime);

    /// <summary>The inverse of the distribution function at the section's exercise time.</summary>
    /// <param name="p">The probability, in (0, 1).</param>
    /// <returns>The logarithm of the underlying.</returns>
    /// <exception cref="ArgumentException">The probability is outside (0, 1).</exception>
    /// <exception cref="NanoQLException">The quantile table couldn't be built.</exception>
    public double InvCdf(double p) => InvCdf(p, smile.ExerciseTime);

    private static string Invariant(FormattableString s) => s.ToString(CultureInfo.InvariantCulture);

    // P(S > K) for a call, P(S <= K) for a put. Each tail is taken from its own option type rather than as 1 - p, so
    // the small number the wings need is never the difference of two numbers close to one. The gap scales with the
    // strike, since the grid is walked outward over decades and a width suiting the forward is far too coarse by the
    // wings. It stays well above the underflow floor that digitalOptionPrice applies for itself.
    private static double TailProbability(SmileSection smile, double strike, OptionType type)
    {
        const double RelativeGap = 2.0e-6;
        double gap = Math.Max(Math.Abs(strike), QL_EPSILON) * RelativeGap;
        return Math.Clamp(smile.DigitalOptionPrice(strike, type, 1.0, gap), 0.0, 1.0);
    }

    // z = Phi^{-1}(P(S <= K)) at log-moneyness x, or null, QuantLib's Null<Real>(), if unusable.
    private static double? NormalQuantileAt(SmileSection smile, double forward, double x)
    {
        var invPhi = new InverseCumulativeNormal();
        double strike = forward * Math.Exp(x);
        if (!double.IsFinite(strike) || strike <= 0.0)
        {
            return null;
        }

        double p = (x <= 0.0) ? TailProbability(smile, strike, OptionType.Put) : TailProbability(smile, strike, OptionType.Call);
        if (p <= 0.0 || p >= 1.0)
        {
            return null;
        }

        double zx = (x <= 0.0) ? invPhi.Evaluate(p) : -invPhi.Evaluate(p);
        return double.IsFinite(zx) ? zx : null;
    }

    private static double[] Linspace(double lo, double hi, int n)
    {
        var v = new double[n];
        for (int i = 0; i < n; ++i)
        {
            v[i] = lo + ((hi - lo) * i / (n - 1));
        }

        return v;
    }

    private void CheckTime(double t)
    {
        double tRef = smile.ExerciseTime;
        if (!ComparisonFunctions.CloseEnough(t, tRef))
        {
            throw new ArgumentException(
                Invariant($"SmileSectionRNDCalculator: requested t={t} does not match smile exercise time {tRef}"), nameof(t));
        }
    }

    // QuantLib's initialize: the quantile table
    private (double Forward, ImmutableArray<double> Z, ImmutableArray<double> LogMoneyness, MonotonicCubicNaturalSpline Fn) Initialize()
    {
        double fwd = smile.AtmLevel()
            ?? throw new NanoQLException(
                "SmileSectionRNDCalculator: smile->atmLevel() returned Null<Real>(); wrap with AtmSmileSection to supply one");

        double t = smile.ExerciseTime;
        double logStd = smile.Volatility(fwd) * Math.Sqrt(t);
        if (!(logStd > 0.0))
        {
            throw new NanoQLException(Invariant($"SmileSectionRNDCalculator: non-positive at-the-money volatility {logStd}"));
        }

        // Fat wings reach a given quantile much further out in strike than a lognormal does, so the extent has to be
        // found rather than assumed.
        double zAtm = NormalQuantileAt(smile, fwd, 0.0)
            ?? throw new NanoQLException("SmileSectionRNDCalculator: could not determine the quantile at the forward");

        // Doubled while out of reach, since a step tuned to a low ATM vol cannot cross fat wings.
        double firstStep = 0.5 * logStd;

        // Ample for doubling out and backing off again.
        const int MaxSteps = 200;

        // Finer than the tabulated map can resolve.
        const double XTol = 1e-12;

        double Edge(double target)
        {
            double x = 0.0;
            double step = firstStep;
            double direction = target > zAtm ? 1.0 : -1.0;
            for (int i = 0; i < MaxSteps; ++i)
            {
                double trial = x + (direction * step);
                double? trialZ = NormalQuantileAt(smile, fwd, trial);
                if (trialZ is not double tz)
                {
                    // Overshot past where the smile still prices, so back off.
                    step *= 0.5;
                    if (step < XTol)
                    {
                        break;
                    }

                    continue;
                }

                bool bracketed = direction > 0.0 ? tz >= target : tz <= target;
                if (bracketed)
                {
                    // z increases with x, so this brackets either way.
                    double xLo = Math.Min(x, trial);
                    double xHi = Math.Max(x, trial);
                    while (xHi - xLo > XTol)
                    {
                        double xMid = 0.5 * (xLo + xHi);
                        if (xMid <= xLo || xMid >= xHi)
                        {
                            break; // down to one representable step
                        }

                        double zMid = NormalQuantileAt(smile, fwd, xMid)
                            ?? throw new NanoQLException("SmileSectionRNDCalculator: could not refine a tail quantile");
                        if (zMid < target)
                        {
                            xLo = xMid;
                        }
                        else
                        {
                            xHi = xMid;
                        }
                    }

                    return 0.5 * (xLo + xHi);
                }

                x = trial;
                step *= 2.0;
            }

            return x;
        }

        double xMax = Edge(nStd);
        double xMin = Edge(-nStd);
        if (!(xMax > xMin))
        {
            throw new NanoQLException(
                "SmileSectionRNDCalculator: could not bracket the quantile range; the smile returns no usable tail probability");
        }

        // Sample the map, keeping it strictly increasing so the spline gets a valid abscissa.
        void Sample(double[] xs, List<double> zOut, List<double> xOut)
        {
            const double DedupTol = 1e-12;
            zOut.Clear();
            xOut.Clear();
            double lastZ = -double.MaxValue;
            foreach (double x in xs)
            {
                if (NormalQuantileAt(smile, fwd, x) is not double zx)
                {
                    continue;
                }

                if (zx > lastZ + DedupTol)
                {
                    zOut.Add(zx);
                    xOut.Add(x);
                    lastZ = zx;
                }
            }
        }

        // Whatever consumes this samples in z, not in strike, so a grid uniform in log-moneyness puts its nodes in the
        // wrong place once the smile has wings. Locate the map coarsely, then re-place the nodes uniformly in z. The
        // locating pass only resolves the shape, not the node placement.
        const int MaxCoarseNodes = 100;
        int nCoarse = Math.Min(MaxCoarseNodes, nStrikes);
        var zCoarse = new List<double>();
        var xCoarse = new List<double>();
        Sample(Linspace(xMin, xMax, nCoarse), zCoarse, xCoarse);
        if (!(zCoarse.Count >= 4))
        {
            throw new NanoQLException(
                Invariant($"SmileSectionRNDCalculator: too few usable quantile points ({zCoarse.Count}) in the locating pass"));
        }

        var coarse = new MonotonicCubicNaturalSpline([.. zCoarse], [.. xCoarse]);
        double[] targets = Linspace(zCoarse[0], zCoarse[^1], nStrikes);
        for (int i = 0; i < targets.Length; ++i)
        {
            targets[i] = coarse.Evaluate(targets[i]);
        }

        var zs = new List<double>();
        var xs = new List<double>();
        Sample(targets, zs, xs);
        if (!(zs.Count >= 4))
        {
            throw new NanoQLException(
                Invariant($"SmileSectionRNDCalculator: too few usable quantile points ({zs.Count}) after deduplication"));
        }

        return (fwd, [.. zs], [.. xs], new MonotonicCubicNaturalSpline([.. zs], [.. xs]));
    }
}
