// SPDX-FileCopyrightText: Copyright (c) 2007, 2013 John Maddock
// SPDX-FileCopyrightText: Copyright Christopher Kormanyos 2013.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/math/special_functions/bessel.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Math's Bessel functions (<c>bessel.hpp</c>): the one nanoql calls, <c>cyl_bessel_i</c>, the modified Bessel
/// function of the first kind I_v(x) of real order, with <c>detail::cyl_bessel_i_imp</c> and its final dispatch.
/// </summary>
/// <remarks>
/// Boost evaluates a <c>double</c> call in <c>long double</c>, MSVC's <c>double</c>, so the errors raised inside name
/// <c>long double</c>; the last check, that the result fits the caller's type, names <c>double</c>. Under the default
/// policy a negative argument with a non-integer order (a domain error on the caller's argument) throws
/// <see cref="ArgumentOutOfRangeException"/>; an overflow and an evaluation error throw <see cref="NanoQLException"/>,
/// each with Boost's message.
/// </remarks>
internal static class BesselFunctions
{
    // tools::log_max_value<long double>(), MSVC's double's
    private const double LogMaxValue = 709.0;

    /// <summary><c>cyl_bessel_i(v, x)</c>: I_v(x).</summary>
    /// <param name="v">The order.</param>
    /// <param name="x">The argument; negative only with an integer order.</param>
    /// <returns>I_v(x).</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="x"/> is negative and <paramref name="v"/> isn't an
    /// integer.</exception>
    /// <exception cref="NanoQLException">The value overflows, or a series runs out of terms.</exception>
    internal static double CylBesselI(double v, double x)
    {
        double result = CylBesselIImp(v, x);

        // policies::checked_narrowing_cast<double>: long double is double here, so only an infinite result is caught
        if (Math.Abs(result) > double.MaxValue)
        {
            throw new NanoQLException("Error in function boost::math::cyl_bessel_i<double>(double,double): Overflow Error");
        }

        return result;
    }

    // detail::cyl_bessel_i_imp: a negative argument by the parity of an integer order
    private static double CylBesselIImp(double v, double x)
    {
        const string function = "boost::math::cyl_bessel_i<long double>(long double,long double)";

        if (x < 0)
        {
            // better have integer v:
            if (Math.Floor(v) == v)
            {
                double r = CylBesselIImpFinal(v, -x);
                if ((RoundFunctions.IRound(v) & 1) != 0)
                {
                    r = -r;
                }

                return r;
            }
            else
            {
                throw new ArgumentOutOfRangeException(
                    nameof(x),
                    $"Error in function {function}: Got x = {x.ToString(CultureInfo.InvariantCulture)}, but we need x >= 0");
            }
        }

        return CylBesselIImpFinal(v, x);
    }

    // detail::cyl_bessel_i_imp_final: the special cases, I0 and I1, the small-argument series, and Temme's method
    private static double CylBesselIImpFinal(double v, double x)
    {
        //
        // This handles all the bessel I functions, note that we don't optimise
        // for integer v, other than the v = 0 or 1 special cases, as Millers
        // algorithm is at least as inefficient as the general case (the general
        // case has better error handling too).
        //
        const string function = "boost::math::cyl_bessel_i<long double>(long double,long double)";
        if (x == 0)
        {
            if (v < 0)
            {
                return Math.Floor(v) == v ? 0 : throw new NanoQLException($"Error in function {function}: Overflow Error");
            }

            return (v == 0) ? 1 : 0;
        }

        if (v == 0.5)
        {
            // common special case, note try and avoid overflow in exp(x):
            if (x >= LogMaxValue)
            {
                double e = Math.Exp(x / 2);
                return e * (e / Math.Sqrt(2 * x * Math.PI));
            }

            return Math.Sqrt(2 / (x * Math.PI)) * Math.Sinh(x);
        }

        // policies::digits<long double>() is 53, at most 113: Boost's I0 and I1
        if (v == 0)
        {
            return BesselI0Functions.BesselI0(x);
        }

        if (v == 1)
        {
            return BesselI1Functions.BesselI1(x);
        }

        if ((v > 0) && (x / v < 0.25))
        {
            return BesselIkFunctions.BesselISmallZSeries(v, x);
        }

        BesselIkFunctions.BesselIk(v, x, BesselIkFunctions.NeedI, out double resultI, out _);
        return resultI;
    }
}
