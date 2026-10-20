// SPDX-FileCopyrightText: (C) Copyright John Maddock 2006.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/math/tools/minima.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Math's <c>brent_find_minima</c> (<c>tools/minima.hpp</c>) for <c>double</c>: Brent's method for a minimum of
/// a function on an interval, by parabolic steps where they are acceptable and golden-section steps otherwise.
/// </summary>
internal static class MinimaFunctions
{
    // golden ratio, don't need too much precision here! Boost's float literal, widened.
    private const double Golden = 0.3819660f;

    /// <summary>The minimum of <paramref name="f"/> on [min, max], with no bound on the iterations.</summary>
    /// <param name="f">The function.</param>
    /// <param name="min">The interval's lower end.</param>
    /// <param name="max">The interval's upper end.</param>
    /// <param name="digits">The precision, in bits, at most half a double's 53.</param>
    /// <returns>The abscissa of the minimum and the function's value there.</returns>
    internal static (double X, double Fx) BrentFindMinima(Func<double, double> f, double min, double max, int digits)
    {
        ulong m = ulong.MaxValue;
        return BrentFindMinima(f, min, max, digits, ref m);
    }

    /// <summary>The minimum of <paramref name="f"/> on [min, max].</summary>
    /// <param name="f">The function.</param>
    /// <param name="min">The interval's lower end.</param>
    /// <param name="max">The interval's upper end.</param>
    /// <param name="bits">The precision, in bits, at most half a double's 53.</param>
    /// <param name="maxIter">The maximum number of iterations; on return, the number taken.</param>
    /// <returns>The abscissa of the minimum and the function's value there.</returns>
    internal static (double X, double Fx) BrentFindMinima(
        Func<double, double> f, double min, double max, int bits, ref ulong maxIter)
    {
        // policies::digits<double, policy<>>() is 53
        bits = Min(53 / 2, bits);
        double tolerance = Math.ScaleB(1.0, 1 - bits);
        double x;  // minima so far
        double w;  // second best point
        double v;  // previous value of w
        double u;  // most recent evaluation point
        double delta;  // The distance moved in the last step
        double delta2; // The distance moved in the step before last
        double fu, fv, fw, fx;  // function evaluations at u, v, w, x
        double mid; // midpoint of min and max
        double fract1, fract2;  // minimal relative movement in x

        x = w = v = max;
        fw = fv = fx = f(x);
        delta2 = delta = 0;

        ulong count = maxIter;

        do
        {
            // get midpoint
            mid = (min + max) / 2;

            // work out if we're done already:
            fract1 = (tolerance * Math.Abs(x)) + (tolerance / 4);
            fract2 = 2 * fract1;
            if (Math.Abs(x - mid) <= (fract2 - ((max - min) / 2)))
            {
                break;
            }

            if (Math.Abs(delta2) > fract1)
            {
                // try and construct a parabolic fit:
                double r = (x - w) * (fx - fv);
                double q = (x - v) * (fx - fw);
                double p = ((x - v) * q) - ((x - w) * r);
                q = 2 * (q - r);
                if (q > 0)
                {
                    p = -p;
                }

                q = Math.Abs(q);
                double td = delta2;
                delta2 = delta;

                // determine whether a parabolic step is acceptable or not:
                if ((Math.Abs(p) >= Math.Abs(q * td / 2)) || (p <= q * (min - x)) || (p >= q * (max - x)))
                {
                    // nope, try golden section instead
                    delta2 = (x >= mid) ? min - x : max - x;
                    delta = Golden * delta2;
                }
                else
                {
                    // whew, parabolic fit:
                    delta = p / q;
                    u = x + delta;
                    if (((u - min) < fract2) || ((max - u) < fract2))
                    {
                        delta = (mid - x) < 0 ? -Math.Abs(fract1) : Math.Abs(fract1);
                    }
                }
            }
            else
            {
                // golden section:
                delta2 = (x >= mid) ? min - x : max - x;
                delta = Golden * delta2;
            }

            // update current position:
            u = (Math.Abs(delta) >= fract1) ? x + delta : (delta > 0 ? x + Math.Abs(fract1) : x - Math.Abs(fract1));
            fu = f(u);
            if (fu <= fx)
            {
                // good new point is an improvement!
                // update brackets:
                if (u >= x)
                {
                    min = x;
                }
                else
                {
                    max = x;
                }

                // update control points:
                v = w;
                w = x;
                x = u;
                fv = fw;
                fw = fx;
                fx = fu;
            }
            else
            {
                // Oh dear, point u is worse than what we have already,
                // even so it *must* be better than one of our endpoints:
                if (u < x)
                {
                    min = u;
                }
                else
                {
                    max = u;
                }

                if ((fu <= fw) || (w == x))
                {
                    // however it is at least second best:
                    v = w;
                    w = u;
                    fv = fw;
                    fw = fu;
                }
                else if ((fu <= fv) || (v == x) || (v == w))
                {
                    // third best:
                    v = u;
                    fv = fu;
                }
            }
        }
        while (--count != 0);

        maxIter -= count;

        return (x, fx);
    }

    private static int Min(int a, int b) => b < a ? b : a;
}
