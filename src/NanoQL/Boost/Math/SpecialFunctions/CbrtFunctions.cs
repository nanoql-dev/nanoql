// SPDX-FileCopyrightText: (C) Copyright John Maddock 2006.
// SPDX-FileCopyrightText: (C) Copyright Matt Borland 2024.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/math/special_functions/cbrt.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Math's cube root (<c>cbrt.hpp</c>) for <c>double</c> under the default policy: a polynomial guess on the
/// mantissa, scaled by the exponent's third, then Halley's iteration to 2^-19, Boost's own route rather than the C
/// runtime's.
/// </summary>
internal static class CbrtFunctions
{
    // the cbrt approximation for z in [0.5, 1]
    private static readonly double[] P =
    [
        0.37568269008611818,
        1.3304968705558024,
        -1.4897101632445036,
        1.2875573098219835,
        -0.6398703759826468,
        0.13584489959258635,
    ];

    private static readonly double[] Correction =
    [
        0.62996052494743658238360530363911,  // 2^-2/3
        0.79370052598409973737585281963615,  // 2^-1/3
        1,
        1.2599210498948731647672106072782,   // 2^1/3
        1.5874010519681994747517056392723,   // 2^2/3
    ];

    /// <summary><c>cbrt(z)</c>: the cube root.</summary>
    /// <param name="z">The argument.</param>
    /// <returns>∛z.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="z"/> is NaN.</exception>
    internal static double Cbrt(double z)
    {
        //
        // cbrt approximation for z in the range [0.5,1]
        // It's hard to say what number of terms gives the optimum
        // trade off between precision and performance, this seems
        // to be about the best for double precision.
        //
        // Maximum Deviation Found:                     1.231e-006
        // Expected Error Term:                         -1.231e-006
        // Maximum Relative Change in Control Points:   5.982e-004
        //
        if (double.IsInfinity(z) || (z == 0))
        {
            return z;
        }

        if (!double.IsFinite(z))
        {
            throw new ArgumentOutOfRangeException(
                nameof(z),
                "Error in function boost::math::cbrt<long double>(long double): Argument to function must be finite but got "
                + z.ToString(CultureInfo.InvariantCulture) + ".");
        }

        int iExp, sign = 1;
        if (z < 0)
        {
            z = -z;
            sign = -sign;
        }

        double guess = Frexp(z, out iExp);
        int originalIExp = iExp; // save for later
        guess = PolynomialHorner2Functions.EvaluatePolynomial(P, guess);
        int iExp3 = iExp / 3;

        // the shift's type, std::uintmax_t, of 64 digits
        if (Math.Abs(iExp3) < 64)
        {
            if (iExp3 > 0)
            {
                guess *= 1UL << iExp3;
            }
            else
            {
                guess /= 1UL << -iExp3;
            }
        }
        else
        {
            guess = Math.ScaleB(guess, iExp3);
        }

        iExp %= 3;
        guess *= Correction[iExp + 2];

        //
        // Now inline Halley iteration.
        // We do this here rather than calling tools::halley_iterate since we can
        // simplify the expressions algebraically, and don't need most of the error
        // checking of the boilerplate version as we know in advance that the function
        // is well behaved...
        //
        // the policy's epsilon at digits2<53 / 3 + 3>: 2^-19
        double eps = 1.0 / (1UL << 19);
        double diff;

        // std::numeric_limits<double>::max_exponent - 3
        if (originalIExp < 1024 - 3)
        {
            //
            // Safe from overflow, use the fast method:
            //
            do
            {
                double g3 = guess * guess * guess;
                diff = (g3 + z + z) / (g3 + g3 + z);
                guess *= diff;
            }
            while (Math.Abs(1 - diff) > eps);
        }
        else
        {
            //
            // Either we're ready to overflow, or we can't tell because numeric_limits isn't
            // available for type T:
            //
            do
            {
                double g2 = guess * guess;
                diff = (g2 - (z / guess)) / ((2 * guess) + (z / g2));
                guess -= diff;
            }
            while ((guess * eps) < Math.Abs(diff));
        }

        return sign * guess;
    }

    // std::frexp for a finite, nonzero x: the mantissa in [0.5, 1) and the exponent, x = mantissa·2^exponent.
    private static double Frexp(double x, out int exponent)
    {
        exponent = Math.ILogB(x) + 1;
        return Math.ScaleB(x, -exponent);
    }
}
