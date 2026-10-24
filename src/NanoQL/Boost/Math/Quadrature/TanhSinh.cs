// SPDX-FileCopyrightText: Copyright Nick Thompson, 2017
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/math/quadrature/tanh_sinh.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Math's <c>tanh_sinh</c> (<c>quadrature/tanh_sinh.hpp</c>) for <c>double</c>: tanh-sinh quadrature on the real
/// line, exponentially convergent for integrands holomorphic on the unit disk (Mori 1985; Bailey, Jeyabalan and Li
/// 2005), with the changes of variables that map a finite, half-infinite or infinite interval onto (−1, 1).
/// </summary>
/// <remarks>
/// Boost's one-argument integrand form, the one QuantLib calls. Its domain error on bounds that aren't sensible throws
/// <see cref="ArgumentOutOfRangeException"/> with Boost's message; its evaluation errors throw
/// <see cref="NanoQLException"/> (<see cref="TanhSinhDetail"/>).
/// </remarks>
internal sealed class TanhSinh
{
    private const string Function = "tanh_sinh<double>::integrate";

    // tools::min_value<double>() and max_value<double>()
    private const double MinValue = 2.2250738585072014E-308;
    private const double MaxValue = double.MaxValue;

    private readonly TanhSinhDetail imp;

    /// <summary>Initializes a new instance of the <see cref="TanhSinh"/> class.</summary>
    /// <param name="maxRefinements">The most refinements; Boost's default is 15.</param>
    /// <param name="minComplement">The smallest complement an abscissa keeps; Boost's default is
    /// 4·<c>DBL_MIN</c>.</param>
    internal TanhSinh(int maxRefinements = 15, double minComplement = MinValue * 4) =>
        imp = new TanhSinhDetail(maxRefinements, minComplement);

    /// <summary>The integral of <paramref name="f"/> over [<paramref name="a"/>, <paramref name="b"/>].</summary>
    /// <param name="f">The integrand.</param>
    /// <param name="a">The lower bound, finite or −<see cref="double.MaxValue"/> and below for −∞.</param>
    /// <param name="b">The upper bound, finite or <see cref="double.MaxValue"/> and above for ∞.</param>
    /// <param name="tolerance">The tolerance, relative to the integral of |f|.</param>
    /// <param name="error">The error estimate; 0 where the bounds are equal, where Boost leaves it unwritten.</param>
    /// <param name="l1">The integral of |f|; 0 where the bounds are equal.</param>
    /// <param name="levels">The number of levels the quadrature reached; 0 where the bounds are equal.</param>
    /// <returns>The integral.</returns>
    /// <exception cref="ArgumentOutOfRangeException">A bound is NaN, or the interval isn't one Boost maps.</exception>
    /// <exception cref="NanoQLException">An evaluation error of the quadrature.</exception>
    internal double Integrate(
        Func<double, double> f, double a, double b, double tolerance, out double error, out double l1, out int levels)
    {
        if (!double.IsNaN(a) && !double.IsNaN(b))
        {
            if ((a <= -MaxValue) && (b >= MaxValue))
            {
                double U(double t, double tc)
                {
                    double tSq = t * t;
                    double inv;
                    if (t > 0.5)
                    {
                        inv = 1 / ((2 - tc) * tc);
                    }
                    else if (t < -0.5)
                    {
                        inv = 1 / ((2 + tc) * -tc);
                    }
                    else
                    {
                        inv = 1 / (1 - tSq);
                    }

                    return f(t * inv) * (1 + tSq) * inv * inv;
                }

                double limit = Math.Sqrt(MinValue) * 4;
                return imp.Integrate(U, out error, out l1, limit, limit, tolerance, out levels);
            }

            if (double.IsFinite(a) && (b >= MaxValue))
            {
                double U(double t, double tc)
                {
                    double z, arg;
                    if (t > -0.5)
                    {
                        z = 1 / (t + 1);
                    }
                    else
                    {
                        z = -1 / tc;
                    }

                    if (t < 0.5)
                    {
                        arg = (2 * z) + a - 1;
                    }
                    else
                    {
                        arg = a + (tc / (2 - tc));
                    }

                    return f(arg) * z * z;
                }

                double leftLimit = Math.Sqrt(MinValue) * 4;
                double q = 2.0 * imp.Integrate(U, out error, out l1, leftLimit, MinValue, tolerance, out levels);
                l1 *= 2;
                error *= 2;
                return q;
            }

            if (double.IsFinite(b) && (a <= -MaxValue))
            {
                double V(double t, double tc)
                {
                    double z;
                    if (t > -0.5)
                    {
                        z = 1 / (t + 1);
                    }
                    else
                    {
                        z = -1 / tc;
                    }

                    double arg;
                    if (t < 0.5)
                    {
                        arg = (2 * z) - 1;
                    }
                    else
                    {
                        arg = tc / (2 - tc);
                    }

                    return f(b - arg) * z * z;
                }

                double leftLimit = Math.Sqrt(MinValue) * 4;
                double q = 2.0 * imp.Integrate(V, out error, out l1, leftLimit, MinValue, tolerance, out levels);
                l1 *= 2;
                error *= 2;
                return q;
            }

            if (double.IsFinite(a) && double.IsFinite(b))
            {
                if (a == b)
                {
                    error = 0;
                    l1 = 0;
                    levels = 0;
                    return 0;
                }

                if (b < a)
                {
                    return -Integrate(f, b, a, tolerance, out error, out l1, out levels);
                }

                double avg = (a + b) * 0.5;
                double diff = (b - a) * 0.5;
                double avgOverDiffM1 = a / diff;
                double avgOverDiffP1 = b / diff;
                bool haveSmallLeft = Math.Abs(a) < 0.5;
                bool haveSmallRight = Math.Abs(b) < 0.5;
                double leftMinComplement = NextFunctions.FloatNext(avgOverDiffM1) - avgOverDiffM1;
                double next = NextFunctions.FloatNext(MinValue / diff);
                double minComplementLimit = MinValue < next ? next : MinValue;
                if (leftMinComplement < minComplementLimit)
                {
                    leftMinComplement = minComplementLimit;
                }

                double rightMinComplement = avgOverDiffP1 - NextFunctions.FloatPrior(avgOverDiffP1);
                if (rightMinComplement < minComplementLimit)
                {
                    rightMinComplement = minComplementLimit;
                }

                double U(double z, double zc)
                {
                    double position;
                    if (z < -0.5)
                    {
                        if (haveSmallLeft)
                        {
                            return f(diff * (avgOverDiffM1 - zc));
                        }

                        position = a - (diff * zc);
                    }
                    else if (z > 0.5)
                    {
                        if (haveSmallRight)
                        {
                            return f(diff * (avgOverDiffP1 - zc));
                        }

                        position = b - (diff * zc);
                    }
                    else
                    {
                        position = avg + (diff * z);
                    }

                    return f(position);
                }

                double q = diff * imp.Integrate(
                    U, out error, out l1, leftMinComplement, rightMinComplement, tolerance, out levels);
                l1 *= diff;
                error *= diff;
                return q;
            }
        }

        throw new ArgumentOutOfRangeException(
            nameof(a),
            $"Error in function {Function}: The domain of integration is not sensible; please check the bounds.");
    }
}
