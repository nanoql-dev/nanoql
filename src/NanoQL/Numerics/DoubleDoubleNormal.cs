// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

namespace NanoQL.Numerics;

/// <summary>
/// The standard normal cumulative distribution in double-double, about 31 digits, in which the full lookback engines'
/// cancelling probabilities evaluate below their cut: double-double arithmetic over double-precision probabilities
/// would keep their 1e-16 error.
/// </summary>
/// <remarks>
/// <para>
/// N(x) = erfc(−x/√2)/2. Below z = 3, erfc(z) = 1 − erf(z), with erf summed by its series of positive terms,
/// erf(z) = (2/√π) e^{−z²} Σ 2ⁿ z^{2n+1} / (1·3⋯(2n+1)) (Abramowitz and Stegun 7.1.6), until a term falls below 2⁻¹¹²
/// of the sum, at most 66 terms. From z = 3, erfc(z) = (e^{−z²}/√π) / (z + (1/2)/(z + 1/(z + (3/2)/(z + ⋯)))) (A&amp;S
/// 7.1.14), 120 terms evaluated from the tail, which reach 1e-33 relative at z = 3 and more digits above.
/// </para>
/// <para>
/// A positive x gives 1 − erfc(z)/2, to about 1e-31 relative; a negative one erfc(z)/2, to about 1e-32 absolute
/// below z = 3 and from it to about 1e-30 relative, the exponential's and the last products' rounding; beyond z = 26,
/// where e^{−z²} leaves double's normal range, 0 or 1. Checked against mpmath's at 50 digits
/// (<c>Conventions/DoubleDoubleNormalTests.cs</c>).
/// </para>
/// </remarks>
internal static class DoubleDoubleNormal
{
    // 1/√2, 2/√π and 1/√π, each its double and the remainder's
    private static readonly DoubleDouble InverseSqrt2 = new(0.7071067811865476, -4.833646656726457e-17);
    private static readonly DoubleDouble TwoOverSqrtPi = new(1.1283791670955126, 1.533545961316588e-17);
    private static readonly DoubleDouble InverseSqrtPi = new(0.5641895835477563, 7.66772980658294e-18);

    /// <summary>The cumulative distribution N(x).</summary>
    /// <param name="x">The argument.</param>
    /// <returns>N(x); a non-finite argument's limit, NaN for NaN.</returns>
    internal static DoubleDouble Cdf(DoubleDouble x)
    {
        if (double.IsNaN(x.Hi))
        {
            return x;
        }

        bool negative = x.Hi < 0.0;
        DoubleDouble halfTail = Erfc((negative ? -x : x) * InverseSqrt2) * 0.5;
        return negative ? halfTail : 1.0 - halfTail;
    }

    // erfc(z) for z ≥ 0
    private static DoubleDouble Erfc(DoubleDouble z)
    {
        if (z.Hi > 26.0)
        {
            return DoubleDouble.Of(0.0);
        }

        DoubleDouble zz = z * z;
        DoubleDouble gauss = DoubleDouble.Exp(-zz);
        if (z.Hi < 3.0)
        {
            DoubleDouble twoZz = zz * 2.0;
            DoubleDouble term = z;
            DoubleDouble sum = z;
            for (int n = 1; Math.Abs(term.Hi) > Math.Abs(sum.Hi) * Math.ScaleB(1.0, -112); ++n)
            {
                term = term * twoZz / ((2.0 * n) + 1.0);
                sum += term;
            }

            return 1.0 - (TwoOverSqrtPi * gauss * sum);
        }

        DoubleDouble fraction = z;
        for (int k = 120; k >= 1; --k)
        {
            fraction = z + ((0.5 * k) / fraction);
        }

        return InverseSqrtPi * gauss / fraction;
    }
}
