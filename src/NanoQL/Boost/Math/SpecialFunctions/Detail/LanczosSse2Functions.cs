// SPDX-FileCopyrightText: (C) Copyright John Maddock 2006.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/math/special_functions/detail/lanczos_sse2.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Math's SSE2 evaluation of the 13m53 Lanczos sums (<c>detail/lanczos_sse2.hpp</c>), to which
/// <c>lanczos13m53</c>'s <c>long double</c> forms forward under MSVC on x64: the numerator and denominator side by side
/// in the two lanes of a register, the even and odd powers in two registers. Each lane's multiply and add is one IEEE
/// double operation, so the scalar steps below, in the same order, give Boost's bits.
/// </summary>
internal static class LanczosSse2Functions
{
    // lanczos_sse2.hpp's coefficients: each pair the numerator's and the denominator's, highest power first
    private static ReadOnlySpan<double> SumCoefficients =>
    [
        2.506628274631000270164908177133837338626, 1.0,
        210.8242777515793458725097339207133627117, 66.0,
        8071.672002365816210638002902272250613822, 1925.0,
        186056.2653952234950402949897160456992822, 32670.0,
        2876370.628935372441225409051620849613599, 357423.0,
        31426415.58540019438061423162831820536287, 2637558.0,
        248874557.8620541565114603864132294232163, 13339535.0,
        1439720407.311721673663223072794912393972, 45995730.0,
        6039542586.35202800506429164430729792107, 105258076.0,
        17921034426.03720969991975575445893111267, 150917976.0,
        35711959237.35566804944018545154716670596, 120543840.0,
        42919803642.64909876895789904700198885093, 39916800.0,
        23531376880.41075968857200767445163675473, 0.0,
    ];

    private static ReadOnlySpan<double> ScaledCoefficients =>
    [
        0.006061842346248906525783753964555936883222, 1.0,
        0.5098416655656676188125178644804694509993, 66.0,
        19.51992788247617482847860966235652136208, 1925.0,
        449.9445569063168119446858607650988409623, 32670.0,
        6955.999602515376140356310115515198987526, 357423.0,
        75999.29304014542649875303443598909137092, 2637558.0,
        601859.6171681098786670226533699352302507, 13339535.0,
        3481712.15498064590882071018964774556468, 45995730.0,
        14605578.08768506808414169982791359218571, 105258076.0,
        43338889.32467613834773723740590533316085, 150917976.0,
        86363131.28813859145546927288977868422342, 120543840.0,
        103794043.1163445451906271053616070238554, 39916800.0,
        56906521.91347156388090791033559122686859, 0.0,
    ];

    /// <summary><c>lanczos13m53::lanczos_sum&lt;long double&gt;</c>.</summary>
    /// <param name="x">The point.</param>
    /// <returns>The sum.</returns>
    internal static double LanczosSum(double x) => Sum(SumCoefficients, x, 4.31965e+25);

    /// <summary><c>lanczos13m53::lanczos_sum_expG_scaled&lt;long double&gt;</c>.</summary>
    /// <param name="x">The point.</param>
    /// <returns>The scaled sum.</returns>
    internal static double LanczosSumExpGScaled(double x) => Sum(ScaledCoefficients, x, 4.76886e+25);

    // lanczos_sse2.hpp's evaluation; lim is the largest x for which the SSE2 code does not go bad, by Boost's experiment
    private static double Sum(ReadOnlySpan<double> coeff, double x, double lim)
    {
        if (x > lim)
        {
            double z = 1 / x;
            return ((((((((((((coeff[24] * z + coeff[22]) * z + coeff[20]) * z + coeff[18]) * z + coeff[16]) * z
                + coeff[14]) * z + coeff[12]) * z + coeff[10]) * z + coeff[8]) * z + coeff[6]) * z + coeff[4]) * z
                + coeff[2]) * z + coeff[0])
                / ((((((((((((coeff[25] * z + coeff[23]) * z + coeff[21]) * z + coeff[19]) * z + coeff[17]) * z
                + coeff[15]) * z + coeff[13]) * z + coeff[11]) * z + coeff[9]) * z + coeff[7]) * z + coeff[5]) * z
                + coeff[3]) * z + coeff[1]);
        }

        // the lanes: [0] the numerator, [1] the denominator
        double x2 = x * x;
        double even0 = coeff[0], even1 = coeff[1];
        double odd0 = coeff[2], odd1 = coeff[3];
        for (int k = 4; k <= 20; k += 4)
        {
            even0 *= x2;
            even1 *= x2;
            odd0 *= x2;
            odd1 *= x2;
            even0 += coeff[k];
            even1 += coeff[k + 1];
            odd0 += coeff[k + 2];
            odd1 += coeff[k + 3];
        }

        even0 *= x2;
        even1 *= x2;
        odd0 *= x;
        odd1 *= x;
        even0 += coeff[24];
        even1 += coeff[25];
        even0 += odd0;
        even1 += odd1;
        return even0 / even1;
    }
}
