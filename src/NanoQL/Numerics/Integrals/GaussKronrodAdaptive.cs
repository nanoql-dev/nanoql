// SPDX-FileCopyrightText: Copyright (C) 2007 François du Vignaud
// SPDX-FileCopyrightText: Copyright (C) 2003 Niels Elken Sønderby
// SPDX-FileCopyrightText: Copyright (C) 2006 François du Vignaud
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/integrals/kronrodintegral.hpp ql/math/integrals/kronrodintegral.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>
/// The adaptive Gauss-Kronrod integration: the 15-point Kronrod rule against its 7-point Gauss rule on an interval,
/// halved, each half to half the tolerance, until the two agree; more robust than the non-adaptive rule on functions
/// that are less smooth, though singular functions need dedicated algorithms, and less efficient, since it doesn't
/// reuse earlier points.
/// </summary>
/// <remarks>
/// <para>
/// References: Gauss-Kronrod Integration
/// (<c>http://mathcssun1.emporia.edu/~oneilcat/ExperimentApplet3/ExperimentApplet3.html</c>); NMS - Numerical Analysis
/// Library (<c>http://www.math.iastate.edu/burkardt/f_src/nms/nms.html</c>). The correctness of the result is tested by
/// checking it against known good values.
/// </para>
/// <para>
/// QuantLib halves the interval by recursion, bounded only by its evaluations, so a tight tolerance overflows a default
/// stack before the bound refuses (<c>kronrodintegral.cpp:386-439</c>), which in .NET ends the process uncaught. The
/// rule iterates over an explicit stack instead, in QuantLib's order: the left half before the right, each interval's
/// value the sum of its halves' as QuantLib's recursion adds them, and the evaluation bound checked before each split.
/// So its values, counts and refusals are QuantLib's on a stack deep enough for QuantLib's recursion. It computes no
/// error estimate, as QuantLib's doesn't: <see cref="IntegrationResult.AbsoluteError"/> is <see langword="null"/>.
/// </para>
/// </remarks>
public sealed class GaussKronrodAdaptive : Integrator
{
    private static readonly double[] G7w = [0.417959183673469, 0.381830050505119, 0.279705391489277, 0.129484966168870];

    private static readonly double[] K15w =
    [
        0.209482141084728,
        0.204432940075298,
        0.190350578064785,
        0.169004726639267,
        0.140653259715525,
        0.104790010322250,
        0.063092092629979,
        0.022935322010529,
    ];

    private static readonly double[] K15t =
    [
        0.000000000000000,
        0.207784955007898,
        0.405845151377397,
        0.586087235467691,
        0.741531185599394,
        0.864864423359769,
        0.949107912342758,
        0.991455371120813,
    ];

    /// <summary>Initializes a new instance of the <see cref="GaussKronrodAdaptive"/> class.</summary>
    /// <param name="tolerance">The absolute tolerance, above machine epsilon.</param>
    /// <param name="maxFunctionEvaluations">The maximum number of evaluations, at least 15; QuantLib's
    /// <c>Null&lt;Size&gt;()</c>, <see cref="int.MaxValue"/>, by default.</param>
    /// <exception cref="ArgumentException"><paramref name="tolerance"/> isn't above machine epsilon, or
    /// <paramref name="maxFunctionEvaluations"/> is below 15.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="maxFunctionEvaluations"/> is negative.</exception>
    public GaussKronrodAdaptive(double tolerance, int maxFunctionEvaluations = int.MaxValue)
        : base(tolerance, maxFunctionEvaluations)
    {
        if (!(maxFunctionEvaluations >= 15))
        {
            throw new ArgumentException(
                $"required maxEvaluations ({maxFunctionEvaluations.ToString(CultureInfo.InvariantCulture)}) not allowed. "
                + "It must be >= 15",
                nameof(maxFunctionEvaluations));
        }
    }

    // The rule on a first estimate: for a non-negative, smooth integrand, the integral of the integrand divided by a
    // first 15-point estimate of it, at an absolute tolerance on that scaled integrand, times the estimate; 0 where the
    // estimate is 0, a 0 estimate being a 0 value for such an integrand, where 0/0 would split to the cap. The rule's
    // absolute tolerance is refused at or below machine epsilon, so an unscaled one can't ask a small integral for its
    // relative digits.
    internal static double IntegrateOnFirstEstimate(
        Func<double, double> f, double a, double b, double relativeTolerance, int maxEvaluations)
    {
        double estimate = new GaussKronrodAdaptive(double.MaxValue, 15).Integrate(f, a, b);
        if (estimate == 0.0)
        {
            return 0.0;
        }

        return new GaussKronrodAdaptive(relativeTolerance, maxEvaluations).Integrate(x => f(x) / estimate, a, b) * estimate;
    }

    /// <inheritdoc/>
    /// <exception cref="NanoQLException">The rule needs more than the maximum number of evaluations.</exception>
    protected override IntegrationResult IntegrateCore(Func<double, double> f, double a, double b)
    {
        // QuantLib's integrateRecursively(f, a, b, absoluteAccuracy()), its calls as frames: a frame evaluates its
        // interval, then, unless the rules agree, its left half and its right half in turn, and adds their values.
        var frames = new Stack<Frame>();
        frames.Push(new Frame(a, b, AbsoluteAccuracy));
        long evaluations = 0;
        double value = 0.0;
        bool returned = false;

        while (frames.Count > 0)
        {
            Frame frame = frames.Pop();
            if (returned)
            {
                // a half's value comes back to its interval
                if (frame.State == 1)
                {
                    frames.Push(frame with { State = 2, Left = value });
                    frames.Push(new Frame(frame.Center, frame.B, frame.Tolerance / 2));
                    returned = false;
                }
                else
                {
                    value = frame.Left + value;
                }

                continue;
            }

            double halflength = (frame.B - frame.A) / 2;
            double center = (frame.A + frame.B) / 2;

            double g7; // will be result of G7 integral
            double k15; // will be result of K15 integral

            double t, fsum; // t (abscissa) and f(t)
            double fc = f(center);
            g7 = fc * G7w[0];
            k15 = fc * K15w[0];

            // calculate g7 and half of k15
            int j, j2;
            for (j = 1, j2 = 2; j < 4; j++, j2 += 2)
            {
                t = halflength * K15t[j2];
                fsum = f(center - t) + f(center + t);
                g7 += fsum * G7w[j];
                k15 += fsum * K15w[j2];
            }

            // calculate other half of k15
            for (j2 = 1; j2 < 8; j2 += 2)
            {
                t = halflength * K15t[j2];
                fsum = f(center - t) + f(center + t);
                k15 += fsum * K15w[j2];
            }

            // multiply by (a - b) / 2
            g7 = halflength * g7;
            k15 = halflength * k15;

            // 15 more function evaluations have been used
            evaluations += 15;

            // error is <= k15 - g7
            // if error is larger than tolerance then split the interval
            // in two and integrate recursively
            if (Math.Abs(k15 - g7) < frame.Tolerance)
            {
                value = k15;
                returned = true;
            }
            else
            {
                if (!(evaluations + 30 <= MaxEvaluations))
                {
                    throw new NanoQLException("maximum number of function evaluations exceeded");
                }

                frames.Push(frame with { State = 1, Center = center });
                frames.Push(new Frame(frame.A, center, frame.Tolerance / 2));
            }
        }

        return new IntegrationResult(value, checked((int)evaluations));
    }

    // One of QuantLib's recursive calls: its interval and tolerance; State 0 before it evaluates, 1 while its left half
    // runs, 2 while its right half runs, Left holding the left half's value.
    private readonly record struct Frame(double A, double B, double Tolerance)
    {
        public int State { get; init; }

        public double Center { get; init; }

        public double Left { get; init; }
    }
}
