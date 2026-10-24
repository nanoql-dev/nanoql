// SPDX-FileCopyrightText: (C) Copyright John Maddock 2006.
// SPDX-FileCopyrightText: (C) Copyright Matt Borland 2024.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/math/tools/roots.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Math's root finders (<c>roots.hpp</c>) that nanoql calls: <c>quadratic_roots</c>, the floating-point branch of
/// <c>detail::quadratic_roots_imp</c> with its <c>detail::discriminant</c>, which
/// <see cref="CubicRootsFunctions.CubicRoots"/> calls; and <c>halley_iterate</c>, the second-order root finder
/// <c>detail::second_order_root_finder</c> with its Halley step, its bracketing and
/// <c>policies::check_root_iterations</c>, which the inverse incomplete gamma calls.
/// </summary>
/// <remarks>
/// Each <c>std::fma</c> is <see cref="Math.FusedMultiplyAdd"/>, MSVC's path; the operations keep Boost's order, so the
/// roots are Boost's bit for bit. A missing root is C++'s <c>quiet_NaN()</c>, whose sign bit is clear. Boost's errors
/// under the default policy throw <see cref="NanoQLException"/> with Boost's message: the range in the wrong order, no
/// root bracketed, and the iterations run out, each an evaluation error.
/// </remarks>
internal static class RootsFunctions
{
    /// <summary><c>policies::get_max_root_iterations</c> under the default policy.</summary>
    internal const ulong MaxRootIterations = 200;

    // std::numeric_limits<double>::quiet_NaN(), 0x7FF8000000000000, where double.NaN has its sign bit set.
    internal static readonly double QuietNaN = BitConverter.Int64BitsToDouble(0x7FF8000000000000);

    // tools::max_value<double>() and tools::min_value<double>(), the largest and the smallest normal value.
    private const double MaxValue = double.MaxValue;
    private const double MinValue = 2.2250738585072014E-308;

    /// <summary>A function with its first two derivatives: <c>halley_iterate</c>'s functor, which returns a tuple.</summary>
    internal interface ISecondOrderFunction
    {
        /// <summary>The function and its first two derivatives at a point.</summary>
        /// <param name="x">The point.</param>
        /// <returns>f(x), f′(x) and f″(x).</returns>
        (double F0, double F1, double F2) Evaluate(double x);
    }

    /// <summary>
    /// <c>tools::halley_iterate(f, guess, min, max, digits, max_iter)</c>: a root of f in [min, max] by Halley's
    /// method, from a guess, to about <paramref name="digits"/> binary digits.
    /// </summary>
    /// <typeparam name="TFunction">The function's type.</typeparam>
    /// <param name="f">The function, with its first two derivatives.</param>
    /// <param name="guess">The first guess.</param>
    /// <param name="min">The range's lower end.</param>
    /// <param name="max">The range's upper end.</param>
    /// <param name="digits">The binary digits to converge to.</param>
    /// <param name="maxIter">The most iterations; on return, the number used.</param>
    /// <returns>The root.</returns>
    /// <exception cref="NanoQLException">The range's ends are in the wrong order, or the steps bracket no root.</exception>
    /// <remarks>
    /// Boost catches <c>std::overflow_error</c> from the function's call at the iterate, and takes the function as
    /// overflowing there; nanoql's Boost ports throw both an overflow and an evaluation error as
    /// <see cref="NanoQLException"/>, so the catch takes every one but Boost's evaluation error of a series out of
    /// terms, which Boost lets through.
    /// </remarks>
    internal static double HalleyIterate<TFunction>(
        TFunction f, double guess, double min, double max, int digits, ref ulong maxIter)
        where TFunction : struct, ISecondOrderFunction
    {
        const string function = "boost::math::tools::halley_iterate<double>";
        if (min >= max)
        {
            throw new NanoQLException(
                $"Error in function {function}: Range arguments in wrong order in "
                + $"boost::math::tools::halley_iterate(first arg={Show(min)})");
        }

        double f0 = 0, f1, f2;
        double result = guess;

        double factor = Math.ScaleB(1.0, 1 - digits);
        double delta = Max(10000000 * guess, 10000000);  // arbitrarily large delta
        double lastF0 = 0;
        double delta1 = delta;
        double delta2 = delta;
        bool outOfBoundsSentry = false;

        //
        // We use these to sanity check that we do actually bracket a root,
        // we update these to the function value when we update the endpoints
        // of the range.  Then, provided at some point we update both endpoints
        // checking that max_range_f * min_range_f <= 0 verifies there is a root
        // to be found somewhere.  Note that if there is no root, and we approach
        // a local minima, then the derivative will go to zero, and hence the next
        // step will jump out of bounds (or at least past the minima), so this
        // check *should* happen in pathological cases.
        //
        double maxRangeF = 0;
        double minRangeF = 0;

        ulong count = maxIter;

        do
        {
            lastF0 = f0;
            delta2 = delta1;
            delta1 = delta;
            try
            {
                (f0, f1, f2) = f.Evaluate(result);
            }
            catch (NanoQLException e) when (!e.Message.Contains("Series evaluation exceeded", StringComparison.Ordinal))
            {
                f0 = max > 0 ? MaxValue : -MinValue;
                f1 = f2 = 0;
            }

            --count;

            if (0 == f0)
            {
                break;
            }

            if (f1 == 0)
            {
                // Oops zero derivative!!!
                HandleZeroDerivative(f, ref lastF0, f0, ref delta, ref result, ref guess, min, max);
            }
            else
            {
                if (f2 != 0)
                {
                    delta = HalleyStep(f0, f1, f2);
                    if (delta * f1 / f0 < 0)
                    {
                        // Oh dear, we have a problem as Newton and Halley steps
                        // disagree about which way we should move.  Probably
                        // there is cancelation error in the calculation of the
                        // Halley step, or else the derivatives are so small
                        // that their values are basically trash.  We will move
                        // in the direction indicated by a Newton step, but
                        // by no more than twice the current guess value, otherwise
                        // we can jump way out of bounds if we're not careful.
                        // See https://svn.boost.org/trac/boost/ticket/8314.
                        delta = f0 / f1;
                        if (Math.Abs(delta) > 2 * Math.Abs(result))
                        {
                            delta = (delta < 0 ? -1 : 1) * 2 * Math.Abs(result);
                        }
                    }
                }
                else
                {
                    delta = f0 / f1;
                }
            }

            // We need to avoid delta/delta2 overflowing here:
            double convergence = (Math.Abs(delta2) > 1) || (Math.Abs(MaxValue * delta2) > Math.Abs(delta))
                ? Math.Abs(delta / delta2)
                : MaxValue;
            if ((convergence > 0.8) && (convergence < 2))
            {
                // last two steps haven't converged.
                if (Math.Abs(min) < 1 ? Math.Abs(1000 * min) < Math.Abs(max) : Math.Abs(max / min) > 1000)
                {
                    if (delta > 0)
                    {
                        delta = BracketRootTowardsMin(f, result, f0, ref min, ref max, ref count);
                    }
                    else
                    {
                        delta = BracketRootTowardsMax(f, result, f0, ref min, ref max, ref count);
                    }
                }
                else
                {
                    delta = (delta > 0) ? (result - min) / 2 : (result - max) / 2;
                    if ((result != 0) && (Math.Abs(delta) > result))
                    {
                        delta = SignFunctions.Sign(delta) * Math.Abs(result) * (double)0.9f; // protect against huge jumps!
                    }
                }

                // reset delta2 so that this branch will *not* be taken on the
                // next iteration:
                delta2 = delta * 3;
                delta1 = delta * 3;
            }

            guess = result;
            result -= delta;

            // check for out of bounds step:
            if (result < min)
            {
                double diff = ((Math.Abs(min) < 1) && (Math.Abs(result) > 1) && (MaxValue / Math.Abs(result) < Math.Abs(min)))
                    ? 1000
                    : (Math.Abs(min) < 1) && (Math.Abs(MaxValue * min) < Math.Abs(result))
                        ? ((min < 0) != (result < 0)) ? -MaxValue : MaxValue
                        : result / min;
                if (Math.Abs(diff) < 1)
                {
                    diff = 1 / diff;
                }

                if (!outOfBoundsSentry && (diff > 0) && (diff < 3))
                {
                    // Only a small out of bounds step, lets assume that the result
                    // is probably approximately at min:
                    delta = (double)0.99f * (guess - min);
                    result = guess - delta;
                    outOfBoundsSentry = true; // only take this branch once!
                }
                else
                {
                    if (FloatDistanceBelowTwo(min, max))
                    {
                        result = guess = (min + max) / 2;
                        break;
                    }

                    delta = BracketRootTowardsMin(f, guess, f0, ref min, ref max, ref count);
                    result = guess - delta;
                    if (result <= min)
                    {
                        result = NextFunctions.FloatNext(min);
                    }

                    if (result >= max)
                    {
                        result = NextFunctions.FloatPrior(max);
                    }

                    guess = min;
                    continue;
                }
            }
            else if (result > max)
            {
                double diff = ((Math.Abs(max) < 1) && (Math.Abs(result) > 1) && (MaxValue / Math.Abs(result) < Math.Abs(max)))
                    ? 1000
                    : result / max;
                if (Math.Abs(diff) < 1)
                {
                    diff = 1 / diff;
                }

                if (!outOfBoundsSentry && (diff > 0) && (diff < 3))
                {
                    // Only a small out of bounds step, lets assume that the result
                    // is probably approximately at min:
                    delta = (double)0.99f * (guess - max);
                    result = guess - delta;
                    outOfBoundsSentry = true; // only take this branch once!
                }
                else
                {
                    if (FloatDistanceBelowTwo(min, max))
                    {
                        result = guess = (min + max) / 2;
                        break;
                    }

                    delta = BracketRootTowardsMax(f, guess, f0, ref min, ref max, ref count);
                    result = guess - delta;
                    if (result >= max)
                    {
                        result = NextFunctions.FloatPrior(max);
                    }

                    if (result <= min)
                    {
                        result = NextFunctions.FloatNext(min);
                    }

                    guess = min;
                    continue;
                }
            }

            // update brackets:
            if (delta > 0)
            {
                max = guess;
                maxRangeF = f0;
            }
            else
            {
                min = guess;
                minRangeF = f0;
            }

            //
            // Sanity check that we bracket the root:
            //
            if (maxRangeF * minRangeF > 0)
            {
                throw new NanoQLException(
                    $"Error in function {function}: There appears to be no root to be found in "
                    + "boost::math::tools::newton_raphson_iterate, perhaps we have a local minima near current best guess "
                    + $"of {Show(guess)}");
            }
        }
        while (count != 0 && (Math.Abs(result * factor) < Math.Abs(delta)));

        maxIter -= count;

        return result;
    }

    /// <summary><c>policies::check_root_iterations</c>: refuses a root finder that ran out of iterations.</summary>
    /// <param name="function">Boost's name of the calling function.</param>
    /// <param name="maxIter">The number of iterations used.</param>
    /// <exception cref="NanoQLException">The root finder used every iteration it was allowed.</exception>
    internal static void CheckRootIterations(string function, ulong maxIter)
    {
        if (maxIter >= MaxRootIterations)
        {
            throw new NanoQLException(
                $"Error in function {function}: Root finding evaluation exceeded {Show((double)maxIter)} iterations, "
                + "giving up now.");
        }
    }

    /// <summary>The real roots of a·x² + b·x + c, in increasing order: <c>quadratic_roots(a, b, c)</c>.</summary>
    /// <param name="a">The coefficient of x².</param>
    /// <param name="b">The coefficient of x.</param>
    /// <param name="c">The constant.</param>
    /// <returns>The two roots, both NaN where there is no real root.</returns>
    internal static (double X0, double X1) QuadraticRoots(double a, double b, double c)
    {
        double nan = QuietNaN;
        if (a == 0)
        {
            if (b == 0 && c != 0)
            {
                return (nan, nan);
            }
            else if (b == 0 && c == 0)
            {
                return (0, 0);
            }

            return (-c / b, -c / b);
        }

        if (b == 0)
        {
            double x0Sq = -c / a;
            if (x0Sq < 0)
            {
                return (nan, nan);
            }

            double x = Math.Sqrt(x0Sq);
            return (-x, x);
        }

        double discriminant = Discriminant(a, b, c);

        // Is there a sane way to flush very small negative values to zero?
        // If there is I don't know of it.
        if (discriminant < 0)
        {
            return (nan, nan);
        }

        double q = -(b + Math.CopySign(Math.Sqrt(discriminant), b)) / 2.0;
        double x0 = q / a;
        double x1 = c / q;
        if (x0 < x1)
        {
            return (x0, x1);
        }

        return (x1, x0);
    }

    // detail::discriminant: b² − 4ac with its two fused products.
    private static double Discriminant(double a, double b, double c)
    {
        double w = 4 * a * c;
        double e = Math.FusedMultiplyAdd(-c, 4 * a, w);
        double f = Math.FusedMultiplyAdd(b, b, -w);
        return f + e;
    }

    // detail::halley_step::step: Halley's step, or Newton's where Halley's would overflow.
    private static double HalleyStep(double f0, double f1, double f2)
    {
        double denom = 2 * f0;
        double num = 2 * f1 - f0 * (f2 / f1);
        double delta;

        if ((Math.Abs(num) < 1) && (Math.Abs(denom) >= Math.Abs(num) * MaxValue))
        {
            // possible overflow, use Newton step:
            delta = f0 / f1;
        }
        else
        {
            delta = denom / num;
        }

        return delta;
    }

    // detail::handle_zero_derivative: a step where the derivative is zero, half the range on the side Boost picks.
    private static void HandleZeroDerivative<TFunction>(
        TFunction f, ref double lastF0, double f0, ref double delta, ref double result, ref double guess, double min, double max)
        where TFunction : struct, ISecondOrderFunction
    {
        if (lastF0 == 0)
        {
            // this must be the first iteration, pretend that we had a
            // previous one at either min or max:
            if (result == min)
            {
                guess = max;
            }
            else
            {
                guess = min;
            }

            lastF0 = f.Evaluate(guess).F0;
            delta = guess - result;
        }

        if (SignFunctions.Sign(lastF0) * SignFunctions.Sign(f0) < 0)
        {
            // we've crossed over so move in opposite direction to last step:
            if (delta < 0)
            {
                delta = (result - min) / 2;
            }
            else
            {
                delta = (result - max) / 2;
            }
        }
        else
        {
            // move in same direction as last step:
            if (delta < 0)
            {
                delta = (result - max) / 2;
            }
            else
            {
                delta = (result - min) / 2;
            }
        }
    }

    // detail::bracket_root_towards_max: the guess moved towards max until the function changes sign, the range narrowed.
    private static double BracketRootTowardsMax<TFunction>(
        TFunction f, double guess, double f0, ref double min, ref double max, ref ulong count)
        where TFunction : struct, ISecondOrderFunction
    {
        if (count < 2)
        {
            return guess - (max + min) / 2; // Not enough counts left to do anything!!
        }

        //
        // Move guess towards max until we bracket the root, updating min and max as we go:
        //
        int e = Math.Abs(FrexpExponent(max / guess));
        double guess0 = guess;
        double multiplier = e < 64 ? 2 : Math.ScaleB(1.0, e / 32);
        double fCurrent = f0;
        if (Math.Abs(min) < Math.Abs(max))
        {
            while (--count != 0 && ((fCurrent < 0) == (f0 < 0)))
            {
                min = guess;
                guess *= multiplier;
                if (guess > max)
                {
                    guess = max;
                    fCurrent = -fCurrent;  // There must be a change of sign!
                    break;
                }

                multiplier *= e > 1024 ? 8 : 2;
                fCurrent = f.Evaluate(guess).F0;
            }
        }
        else
        {
            //
            // If min and max are negative we have to divide to head towards max:
            //
            while (--count != 0 && ((fCurrent < 0) == (f0 < 0)))
            {
                min = guess;
                guess /= multiplier;
                if (guess > max)
                {
                    guess = max;
                    fCurrent = -fCurrent;  // There must be a change of sign!
                    break;
                }

                multiplier *= e > 1024 ? 8 : 2;
                fCurrent = f.Evaluate(guess).F0;
            }
        }

        if (count != 0)
        {
            max = guess;
            if (multiplier > 16)
            {
                return (guess0 - guess) + BracketRootTowardsMin(f, guess, fCurrent, ref min, ref max, ref count);
            }
        }

        return guess0 - (max + min) / 2;
    }

    // detail::bracket_root_towards_min: the guess moved towards min until the function changes sign, the range narrowed.
    private static double BracketRootTowardsMin<TFunction>(
        TFunction f, double guess, double f0, ref double min, ref double max, ref ulong count)
        where TFunction : struct, ISecondOrderFunction
    {
        if (count < 2)
        {
            return guess - (max + min) / 2; // Not enough counts left to do anything!!
        }

        //
        // Move guess towards min until we bracket the root, updating min and max as we go:
        //
        int e = Math.Abs(FrexpExponent(guess / min));
        double guess0 = guess;
        double multiplier = e < 64 ? 2 : Math.ScaleB(1.0, e / 32);
        double fCurrent = f0;

        if (Math.Abs(min) < Math.Abs(max))
        {
            while (--count != 0 && ((fCurrent < 0) == (f0 < 0)))
            {
                max = guess;
                guess /= multiplier;
                if (guess < min)
                {
                    guess = min;
                    fCurrent = -fCurrent;  // There must be a change of sign!
                    break;
                }

                multiplier *= e > 1024 ? 8 : 2;
                fCurrent = f.Evaluate(guess).F0;
            }
        }
        else
        {
            //
            // If min and max are negative we have to multiply to head towards min:
            //
            while (--count != 0 && ((fCurrent < 0) == (f0 < 0)))
            {
                max = guess;
                guess *= multiplier;
                if (guess < min)
                {
                    guess = min;
                    fCurrent = -fCurrent;  // There must be a change of sign!
                    break;
                }

                multiplier *= e > 1024 ? 8 : 2;
                fCurrent = f.Evaluate(guess).F0;
            }
        }

        if (count != 0)
        {
            min = guess;
            if (multiplier > 16)
            {
                return (guess0 - guess) + BracketRootTowardsMax(f, guess, fCurrent, ref min, ref max, ref count);
            }
        }

        return guess0 - (max + min) / 2;
    }

    // std::frexp's exponent as MSVC's runtime gives it: 0 at zero, -1 at an infinity or a NaN, otherwise the exponent
    // that puts the mantissa in [0.5, 1), subnormals included.
    private static int FrexpExponent(double x) => x == 0 ? 0 : !double.IsFinite(x) ? -1 : Math.ILogB(x) + 1;

    // fabs(float_distance(min, max)) < 2: the two ends equal or adjacent doubles, float_distance counting a zero of
    // either sign as one value; float_distance's domain errors on a non-finite end kept.
    private static bool FloatDistanceBelowTwo(double a, double b)
    {
        const string function = "float_distance<double>(double, double)";
        if (!double.IsFinite(a))
        {
            throw new ArgumentOutOfRangeException(
                nameof(a), $"Error in function {function}: Argument a must be finite, but got {Show(a)}");
        }

        if (!double.IsFinite(b))
        {
            throw new ArgumentOutOfRangeException(
                nameof(b), $"Error in function {function}: Argument b must be finite, but got {Show(b)}");
        }

        // the difference wraps where it passes 2^63, far from the two values either side of 0
        long distance = unchecked(Ordered(b) - Ordered(a));
        return distance > -2 && distance < 2;

        // the doubles in order as integers, −0 and +0 one value
        static long Ordered(double x)
        {
            long bits = BitConverter.DoubleToInt64Bits(x);
            return bits < 0 ? -(bits & long.MaxValue) : bits;
        }
    }

    // std::max as C++ defines it, (a < b) ? b : a: the first argument where the comparison fails, a NaN among them.
    private static double Max(double a, double b) => a < b ? b : a;

    private static string Show(double value) => value.ToString(CultureInfo.InvariantCulture);
}
