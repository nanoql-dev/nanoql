// SPDX-FileCopyrightText: (C) Copyright John Maddock 2006.
// SPDX-FileCopyrightText: (C) Copyright Matt Borland 2024.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/math/tools/toms748_solve.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Math's bracketing root finders (<c>tools/toms748_solve.hpp</c>) for <c>double</c>: <c>toms748_solve</c>,
/// G. E. Alefeld, F. A. Potra and Y. Shi's Algorithm 748 (ACM TOMS 21(3), 1995), with its interpolation steps;
/// <c>bracket_and_solve_root</c>, which brackets a monotone function's root from a guess and then solves it; and
/// <c>eps_tolerance</c>, the termination test on the bracket's relative width.
/// </summary>
/// <remarks>
/// Boost's errors under the default policy throw with Boost's message: a domain error on the solver's arguments (the
/// ends out of order, or not bracketing the root), <see cref="ArgumentOutOfRangeException"/>; an evaluation error (no
/// bracket found), <see cref="NanoQLException"/>.
/// </remarks>
internal static class Toms748SolveFunctions
{
    // tools::epsilon<double>(), tools::min_value<double>(), tools::max_value<double>()
    private const double Epsilon = 2.220446049250313E-16;
    private const double MinValue = 2.2250738585072014E-308;
    private const double MaxValue = double.MaxValue;

    /// <summary>
    /// <c>toms748_solve(f, ax, bx, tol, max_iter)</c>: a root of <paramref name="f"/> in [ax, bx], whose ends'
    /// function values have opposite signs, to the tolerance's width.
    /// </summary>
    /// <param name="f">The function.</param>
    /// <param name="ax">The bracket's lower end.</param>
    /// <param name="bx">The bracket's upper end.</param>
    /// <param name="tol">The termination test.</param>
    /// <param name="maxIter">The most function calls; on return, the number made.</param>
    /// <returns>The final bracket.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The ends are out of order, or don't bracket a root.</exception>
    internal static (double A, double B) Toms748Solve(
        Func<double, double> f, double ax, double bx, EpsTolerance tol, ref ulong maxIter)
    {
        if (maxIter <= 2)
        {
            return (ax, bx);
        }

        maxIter -= 2;
        (double A, double B) r = Toms748Solve(f, ax, bx, f(ax), f(bx), tol, ref maxIter);
        maxIter += 2;
        return r;
    }

    /// <summary>
    /// <c>toms748_solve(f, ax, bx, fax, fbx, tol, max_iter)</c>: a root of <paramref name="f"/> in [ax, bx] from the
    /// ends' function values, which have opposite signs, to the tolerance's width.
    /// </summary>
    /// <param name="f">The function.</param>
    /// <param name="ax">The bracket's lower end.</param>
    /// <param name="bx">The bracket's upper end.</param>
    /// <param name="fax">f(ax).</param>
    /// <param name="fbx">f(bx).</param>
    /// <param name="tol">The termination test.</param>
    /// <param name="maxIter">The most function calls; on return, the number made.</param>
    /// <returns>The final bracket.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The ends are out of order, or don't bracket a root.</exception>
    internal static (double A, double B) Toms748Solve(
        Func<double, double> f, double ax, double bx, double fax, double fbx, EpsTolerance tol, ref ulong maxIter)
    {
        //
        // Main entry point and logic for Toms Algorithm 748
        // root finder.
        //
        const string function = "boost::math::tools::toms748_solve<double>";

        //
        // Sanity check - are we allowed to iterate at all?
        //
        if (maxIter == 0)
        {
            return (ax, bx);
        }

        ulong count = maxIter;
        double a, b, fa, fb, c, u, fu, a0, b0, d, fd, e, fe;
        const double mu = (double)0.5f;

        // initialise a, b and fa, fb:
        a = ax;
        b = bx;
        if (a >= b)
        {
            throw new ArgumentOutOfRangeException(
                nameof(ax), $"Error in function {function}: Parameters a and b out of order: a={Show(a)}");
        }

        fa = fax;
        fb = fbx;

        if (tol.IsMet(a, b) || (fa == 0) || (fb == 0))
        {
            maxIter = 0;
            if (fa == 0)
            {
                b = a;
            }
            else if (fb == 0)
            {
                a = b;
            }

            return (a, b);
        }

        if (SignFunctions.Sign(fa) * SignFunctions.Sign(fb) > 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(bx), $"Error in function {function}: Parameters a and b do not bracket the root: a={Show(a)}");
        }

        // dummy value for fd, e and fe:
        fe = e = fd = (double)1e5F;
        d = 0; // C#'s definite assignment: Boost leaves d unset until the first bracket below, which fa != 0 always takes

        if (fa != 0)
        {
            //
            // On the first step we take a secant step:
            //
            c = SecantInterpolate(a, b, fa, fb);
            Bracket(f, ref a, ref b, c, ref fa, ref fb, ref d, ref fd);
            --count;

            if (count != 0 && (fa != 0) && !tol.IsMet(a, b))
            {
                //
                // On the second step we take a quadratic interpolation:
                //
                c = QuadraticInterpolate(a, b, d, fa, fb, fd, 2);
                e = d;
                fe = fd;
                Bracket(f, ref a, ref b, c, ref fa, ref fb, ref d, ref fd);
                --count;
            }
        }

        while (count != 0 && (fa != 0) && !tol.IsMet(a, b))
        {
            // save our brackets:
            a0 = a;
            b0 = b;

            //
            // Starting with the third step taken
            // we can use either quadratic or cubic interpolation.
            // Cubic interpolation requires that all four function values
            // fa, fb, fd, and fe are distinct, should that not be the case
            // then variable prof will get set to true, and we'll end up
            // taking a quadratic step instead.
            //
            double minDiff = MinValue * 32;
            bool prof = (Math.Abs(fa - fb) < minDiff) || (Math.Abs(fa - fd) < minDiff) || (Math.Abs(fa - fe) < minDiff)
                || (Math.Abs(fb - fd) < minDiff) || (Math.Abs(fb - fe) < minDiff) || (Math.Abs(fd - fe) < minDiff);
            if (prof)
            {
                c = QuadraticInterpolate(a, b, d, fa, fb, fd, 2);
            }
            else
            {
                c = CubicInterpolate(a, b, d, e, fa, fb, fd, fe);
            }

            //
            // re-bracket, and check for termination:
            //
            e = d;
            fe = fd;
            Bracket(f, ref a, ref b, c, ref fa, ref fb, ref d, ref fd);
            if ((0 == --count) || (fa == 0) || tol.IsMet(a, b))
            {
                break;
            }

            //
            // Now another interpolated step:
            //
            prof = (Math.Abs(fa - fb) < minDiff) || (Math.Abs(fa - fd) < minDiff) || (Math.Abs(fa - fe) < minDiff)
                || (Math.Abs(fb - fd) < minDiff) || (Math.Abs(fb - fe) < minDiff) || (Math.Abs(fd - fe) < minDiff);
            if (prof)
            {
                c = QuadraticInterpolate(a, b, d, fa, fb, fd, 3);
            }
            else
            {
                c = CubicInterpolate(a, b, d, e, fa, fb, fd, fe);
            }

            //
            // Bracket again, and check termination condition, update e:
            //
            Bracket(f, ref a, ref b, c, ref fa, ref fb, ref d, ref fd);
            if ((0 == --count) || (fa == 0) || tol.IsMet(a, b))
            {
                break;
            }

            //
            // Now we take a double-length secant step:
            //
            if (Math.Abs(fa) < Math.Abs(fb))
            {
                u = a;
                fu = fa;
            }
            else
            {
                u = b;
                fu = fb;
            }

            c = u - 2 * (fu / (fb - fa)) * (b - a);
            if (Math.Abs(c - u) > (b - a) / 2)
            {
                c = a + (b - a) / 2;
            }

            //
            // Bracket again, and check termination condition:
            //
            e = d;
            fe = fd;
            Bracket(f, ref a, ref b, c, ref fa, ref fb, ref d, ref fd);
            if ((0 == --count) || (fa == 0) || tol.IsMet(a, b))
            {
                break;
            }

            //
            // And finally... check to see if an additional bisection step is
            // to be taken, we do this if we're not converging fast enough:
            //
            if ((b - a) < mu * (b0 - a0))
            {
                continue;
            }

            //
            // bracket again on a bisection:
            //
            e = d;
            fe = fd;
            Bracket(f, ref a, ref b, a + (b - a) / 2, ref fa, ref fb, ref d, ref fd);
            --count;
        }

        maxIter -= count;
        if (fa == 0)
        {
            b = a;
        }
        else if (fb == 0)
        {
            a = b;
        }

        return (a, b);
    }

    /// <summary>
    /// <c>bracket_and_solve_root(f, guess, factor, rising, tol, max_iter)</c>: a root of a monotone function, bracketed
    /// by stepping from the guess by the factor, growing as the steps go on, then solved by
    /// <see cref="Toms748Solve(Func{double, double}, double, double, double, double, EpsTolerance, ref ulong)"/>.
    /// </summary>
    /// <param name="f">The function.</param>
    /// <param name="guess">The first guess.</param>
    /// <param name="factor">The step's first factor, above 1.</param>
    /// <param name="rising">Whether the function rises.</param>
    /// <param name="tol">The termination test.</param>
    /// <param name="maxIter">The most function calls; on return, the number made.</param>
    /// <returns>The final bracket.</returns>
    /// <exception cref="NanoQLException">No bracket was found within the calls allowed.</exception>
    internal static (double A, double B) BracketAndSolveRoot(
        Func<double, double> f, double guess, double factor, bool rising, EpsTolerance tol, ref ulong maxIter)
    {
        const string function = "boost::math::tools::bracket_and_solve_root<double>";

        //
        // Set up initial brackets:
        //
        double a = guess;
        double b = a;
        double fa = f(a);
        double fb = fa;

        //
        // Set up invocation count:
        //
        ulong count = maxIter - 1;

        int step = 32;

        if ((fa < 0) == (guess < 0 ? !rising : rising))
        {
            //
            // Zero is to the right of b, so walk upwards
            // until we find it:
            //
            while (SignFunctions.Sign(fb) == SignFunctions.Sign(fa))
            {
                if (count == 0)
                {
                    throw new NanoQLException(
                        $"Error in function {function}: Unable to bracket root, last nearest value was {Show(b)}");
                }

                //
                // Heuristic: normally it's best not to increase the step sizes as we'll just end up
                // with a really wide range to search for the root.  However, if the initial guess was *really*
                // bad then we need to speed up the search otherwise we'll take forever if we're orders of
                // magnitude out.  This happens most often if the guess is a small value (say 1) and the result
                // we're looking for is close to std::numeric_limits<T>::min().
                //
                if ((maxIter - count) % (uint)step == 0u)
                {
                    factor *= 2;
                    if (step > 1)
                    {
                        step /= 2;
                    }
                }

                //
                // Now go ahead and move our guess by "factor":
                //
                a = b;
                fa = fb;
                b *= factor;
                fb = f(b);
                --count;
            }
        }
        else
        {
            //
            // Zero is to the left of a, so walk downwards
            // until we find it:
            //
            while (SignFunctions.Sign(fb) == SignFunctions.Sign(fa))
            {
                if (Math.Abs(a) < MinValue)
                {
                    // Escape route just in case the answer is zero!
                    maxIter -= count;
                    maxIter += 1;
                    return a > 0 ? (0, a) : (a, 0);
                }

                if (count == 0)
                {
                    throw new NanoQLException(
                        $"Error in function {function}: Unable to bracket root, last nearest value was {Show(a)}");
                }

                //
                // Heuristic: normally it's best not to increase the step sizes as we'll just end up
                // with a really wide range to search for the root.  However, if the initial guess was *really*
                // bad then we need to speed up the search otherwise we'll take forever if we're orders of
                // magnitude out.  This happens most often if the guess is a small value (say 1) and the result
                // we're looking for is close to std::numeric_limits<T>::min().
                //
                if ((maxIter - count) % (uint)step == 0u)
                {
                    factor *= 2;
                    if (step > 1)
                    {
                        step /= 2;
                    }
                }

                //
                // Now go ahead and move are guess by "factor":
                //
                b = a;
                fb = fa;
                a /= factor;
                fa = f(a);
                --count;
            }
        }

        maxIter -= count;
        maxIter += 1;
        (double A, double B) r = Toms748Solve(
            f,
            a < 0 ? b : a,
            a < 0 ? a : b,
            a < 0 ? fb : fa,
            a < 0 ? fa : fb,
            tol,
            ref count);
        maxIter += count;
        return r;
    }

    // detail::bracket: f(c) at a point c inside [a, b], moved off the ends where it lies too close, and the bracket
    // narrowed to the side that keeps the sign change; d and fd the end dropped, the third best guess.
    private static void Bracket(
        Func<double, double> f, ref double a, ref double b, double c, ref double fa, ref double fb, ref double d, ref double fd)
    {
        //
        // Given a point c inside the existing enclosing interval
        // [a, b] sets a = c if f(c) == 0, otherwise finds the new
        // enclosing interval: either [a, c] or [c, b] and sets
        // d and fd to the point that has just been removed from
        // the interval.  In other words d is the third best guess
        // to the root.
        //
        double tol = Epsilon * 2;

        //
        // If the interval [a,b] is very small, or if c is too close
        // to one end of the interval then we need to adjust the
        // location of c accordingly:
        //
        if ((b - a) < 2 * tol * a)
        {
            c = a + (b - a) / 2;
        }
        else if (c <= a + Math.Abs(a) * tol)
        {
            c = a + Math.Abs(a) * tol;
        }
        else if (c >= b - Math.Abs(b) * tol)
        {
            c = b - Math.Abs(b) * tol;
        }

        //
        // OK, lets invoke f(c):
        //
        double fc = f(c);

        //
        // if we have a zero then we have an exact solution to the root:
        //
        if (fc == 0)
        {
            a = c;
            fa = 0;
            d = 0;
            fd = 0;
            return;
        }

        //
        // Non-zero fc, update the interval:
        //
        if (SignFunctions.Sign(fa) * SignFunctions.Sign(fc) < 0)
        {
            d = b;
            fd = fb;
            b = c;
            fb = fc;
        }
        else
        {
            d = a;
            fd = fa;
            a = c;
            fa = fc;
        }
    }

    // detail::safe_div: num / denom, or r where the quotient would overflow
    private static double SafeDiv(double num, double denom, double r)
    {
        //
        // return num / denom without overflow,
        // return r if overflow would occur.
        //
        if (Math.Abs(denom) < 1)
        {
            if (Math.Abs(denom * MaxValue) <= Math.Abs(num))
            {
                return r;
            }
        }

        return num / denom;
    }

    // detail::secant_interpolate: the secant's point, or the midpoint where it falls too close to an end
    private static double SecantInterpolate(double a, double b, double fa, double fb)
    {
        //
        // Performs standard secant interpolation of [a,b] given
        // function evaluations f(a) and f(b).  Performs a bisection
        // if secant interpolation would leave us very close to either
        // a or b.  Rationale: we only call this function when at least
        // one other form of interpolation has already failed, so we know
        // that the function is unlikely to be smooth with a root very
        // close to a or b.
        //
        double tol = Epsilon * 5;
        double c = a - (fa / (fb - fa)) * (b - a);
        if ((c <= a + Math.Abs(a) * tol) || (c >= b - Math.Abs(b) * tol))
        {
            return (a + b) / 2;
        }

        return c;
    }

    // detail::quadratic_interpolate: count Newton steps on the quadratic through a, b and d, or the secant's point
    // where they leave [a, b]
    private static double QuadraticInterpolate(double a, double b, double d, double fa, double fb, double fd, uint count)
    {
        //
        // Performs quadratic interpolation to determine the next point,
        // takes count Newton steps to find the location of the
        // quadratic polynomial.
        //
        // Point d must lie outside of the interval [a,b], it is the third
        // best approximation to the root, after a and b.
        //
        // Note: this does not guarantee to find a root
        // inside [a, b], so we fall back to a secant step should
        // the result be out of range.
        //
        // Start by obtaining the coefficients of the quadratic polynomial:
        //
        double bq = SafeDiv(fb - fa, b - a, MaxValue);
        double aq = SafeDiv(fd - fb, d - b, MaxValue);
        aq = SafeDiv(aq - bq, d - a, 0);

        if (aq == 0)
        {
            // failure to determine coefficients, try a secant step:
            return SecantInterpolate(a, b, fa, fb);
        }

        //
        // Determine the starting point of the Newton steps:
        //
        double c;
        if (SignFunctions.Sign(aq) * SignFunctions.Sign(fa) > 0)
        {
            c = a;
        }
        else
        {
            c = b;
        }

        //
        // Take the Newton steps:
        //
        for (uint i = 1; i <= count; ++i)
        {
            c -= SafeDiv(fa + (bq + aq * (c - b)) * (c - a), bq + aq * (2 * c - a - b), 1 + c - a);
        }

        if ((c <= a) || (c >= b))
        {
            // Oops, failure, try a secant step:
            c = SecantInterpolate(a, b, fa, fb);
        }

        return c;
    }

    // detail::cubic_interpolate: inverse cubic interpolation through a, b, d and e, or the quadratic step where it leaves
    // [a, b]
    private static double CubicInterpolate(
        double a, double b, double d, double e, double fa, double fb, double fd, double fe)
    {
        //
        // Uses inverse cubic interpolation of f(x) at points
        // [a,b,d,e] to obtain an approximate root of f(x).
        // Points d and e lie outside the interval [a,b]
        // and are the third and forth best approximations
        // to the root that we have found so far.
        //
        // Note: this does not guarantee to find a root
        // inside [a, b], so we fall back to quadratic
        // interpolation in case of an erroneous result.
        //
        double q11 = (d - e) * fd / (fe - fd);
        double q21 = (b - d) * fb / (fd - fb);
        double q31 = (a - b) * fa / (fb - fa);
        double d21 = (b - d) * fd / (fd - fb);
        double d31 = (a - b) * fb / (fb - fa);
        double q22 = (d21 - q11) * fb / (fe - fb);
        double q32 = (d31 - q21) * fa / (fd - fa);
        double d32 = (d31 - q21) * fd / (fd - fa);
        double q33 = (d32 - q22) * fa / (fe - fa);
        double c = q31 + q32 + q33 + a;

        if ((c <= a) || (c >= b))
        {
            // Out of bounds step, fall back to quadratic interpolation:
            c = QuadraticInterpolate(a, b, d, fa, fb, fd, 3);
        }

        return c;
    }

    private static string Show(double value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>
    /// <c>tools::eps_tolerance</c>: the bracket [a, b] is narrow enough where |a − b| ≤ ε·min(|a|, |b|), ε 2^(1 − bits)
    /// or 4 ulp, whichever is larger.
    /// </summary>
    internal readonly struct EpsTolerance
    {
        private readonly double eps;

        /// <summary>Initializes a new instance of the <see cref="EpsTolerance"/> struct: <c>eps_tolerance(bits)</c>.</summary>
        /// <param name="bits">The binary digits the bracket must agree to.</param>
        internal EpsTolerance(int bits)
        {
            // ldexp(1.0F, 1 - bits) in float, widened; std::max(a, b) is (a < b) ? b : a
            double a = (double)MathF.ScaleB(1.0F, 1 - bits);
            double b = 4 * Epsilon;
            eps = a < b ? b : a;
        }

        /// <summary>Whether the bracket is narrow enough: <c>operator()(a, b)</c>.</summary>
        /// <param name="a">One end.</param>
        /// <param name="b">The other end.</param>
        /// <returns>Whether |a − b| ≤ ε·min(|a|, |b|).</returns>
        internal bool IsMet(double a, double b)
        {
            // std::min(a, b) is (b < a) ? b : a
            double absA = Math.Abs(a);
            double absB = Math.Abs(b);
            return Math.Abs(a - b) <= (eps * (absB < absA ? absB : absA));
        }
    }
}
