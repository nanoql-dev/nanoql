// SPDX-FileCopyrightText: (C) Copyright John Maddock 2006.
// SPDX-FileCopyrightText: (C) Copyright Matt Borland 2024.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/math/special_functions/detail/lgamma_small.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

using static NanoQL.Numerics.PolynomialHorner2Functions;

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Math's lgamma for small arguments (<c>lgamma_small.hpp</c>): the rational approximations accurate to 64-bit
/// mantissas, which Boost's precision tag selects for a 53-bit type, in [1, 3) after the recurrence.
/// </summary>
internal static class LgammaSmallFunctions
{
    // z in [2, 3): lgamma(z) = (z − 2)(z + 1)(Y + R(z − 2)); at double, max error found 4.231e-18
    private const double Y1 = (double)0.158963680267333984375e0f;

    // z in [1, 1.5]: lgamma(z) = (z − 1)(z − 2)(Y + R(z − 1)); at double, max error found 1.230011e-17
    private const double Y2 = (double)0.52815341949462890625f;

    // z in [1.5, 2]: lgamma(z) = (2 − z)(1 − z)(Y + R(2 − z)); at double, max error found 1.797565e-17
    private const double Y3 = (double)0.452017307281494140625f;

    private static ReadOnlySpan<double> P1 =>
    [
        -0.180355685678449379109e-1,
        0.25126649619989678683e-1,
        0.494103151567532234274e-1,
        0.172491608709613993966e-1,
        -0.259453563205438108893e-3,
        -0.541009869215204396339e-3,
        -0.324588649825948492091e-4,
    ];

    private static ReadOnlySpan<double> Q1 =>
    [
        0.1e1,
        0.196202987197795200688e1,
        0.148019669424231326694e1,
        0.541391432071720958364e0,
        0.988504251128010129477e-1,
        0.82130967464889339326e-2,
        0.224936291922115757597e-3,
        -0.223352763208617092964e-6,
    ];

    private static ReadOnlySpan<double> P2 =>
    [
        0.490622454069039543534e-1,
        -0.969117530159521214579e-1,
        -0.414983358359495381969e0,
        -0.406567124211938417342e0,
        -0.158413586390692192217e0,
        -0.240149820648571559892e-1,
        -0.100346687696279557415e-2,
    ];

    private static ReadOnlySpan<double> Q2 =>
    [
        0.1e1,
        0.302349829846463038743e1,
        0.348739585360723852576e1,
        0.191415588274426679201e1,
        0.507137738614363510846e0,
        0.577039722690451849648e-1,
        0.195768102601107189171e-2,
    ];

    private static ReadOnlySpan<double> P3 =>
    [
        -0.292329721830270012337e-1,
        0.144216267757192309184e0,
        -0.142440390738631274135e0,
        0.542809694055053558157e-1,
        -0.850535976868336437746e-2,
        0.431171342679297331241e-3,
    ];

    private static ReadOnlySpan<double> Q3 =>
    [
        0.1e1,
        -0.150169356054485044494e1,
        0.846973248876495016101e0,
        -0.220095151814995745555e0,
        0.25582797155975869989e-1,
        -0.100666795539143372762e-2,
        -0.827193521891290553639e-6,
    ];

    /// <summary><c>lgamma_small_imp</c> at the 64-bit tag: ln Γ(z) for z in [√ε, 15).</summary>
    /// <param name="z">The argument.</param>
    /// <param name="zm1">z − 1, as the caller computes it.</param>
    /// <param name="zm2">z − 2, as the caller computes it.</param>
    /// <returns>ln Γ(z).</returns>
    internal static double LgammaSmallImp(double z, double zm1, double zm2)
    {
        double result = 0;

        if ((zm1 == 0) || (zm2 == 0))
        {
            // nothing to do, result is zero....
        }
        else if (z > 2)
        {
            //
            // Begin by performing argument reduction until
            // z is in [2,3):
            //
            if (z >= 3)
            {
                do
                {
                    z -= 1;
                    zm2 -= 1;
                    result += Math.Log(z);
                }
                while (z >= 3);

                // Update zm2, we need it below:
                zm2 = z - 2;
            }

            double r = zm2 * (z + 1);
            double rr = EvaluatePolynomial(P1, zm2);
            rr /= EvaluatePolynomial(Q1, zm2);

            result += r * Y1 + r * rr;
        }
        else
        {
            //
            // If z is less than 1 use recurrence to shift to
            // z in the interval [1,2]:
            //
            if (z < 1)
            {
                result += -Math.Log(z);
                zm2 = zm1;
                zm1 = z;
                z += 1;
            }

            //
            // Two approximations, on for z in [1,1.5] and
            // one for z in [1.5,2]:
            //
            if (z <= 1.5)
            {
                double r = EvaluatePolynomial(P2, zm1) / EvaluatePolynomial(Q2, zm1);
                double prefix = zm1 * zm2;

                result += prefix * Y2 + prefix * r;
            }
            else
            {
                double r = zm2 * zm1;
                double rr = EvaluatePolynomial(P3, -zm2) / EvaluatePolynomial(Q3, -zm2);

                result += r * Y3 + r * rr;
            }
        }

        return result;
    }
}
