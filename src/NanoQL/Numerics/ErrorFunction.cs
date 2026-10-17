// SPDX-FileCopyrightText: Copyright (C) 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 1993 by Sun Microsystems, Inc. All rights reserved.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause AND SunPro
// Upstream: ql/math/errorfunction.hpp ql/math/errorfunction.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.
// NOTE: The following copyright notice
// applies only to the (modified) code of erff.
//
//
// erff
// ====
//
// Based on code from the gnu C library, originally written by Sun.
// Modified to remove reliance on features of gcc and 64-bit width
// of doubles. No doubt this results in some slight deterioration
// of efficiency, but this is not really noticeable in testing.
//
//
//
// ====================================================
// Copyright (C) 1993 by Sun Microsystems, Inc. All rights reserved.
//
// Developed at SunPro, a Sun Microsystems, Inc. business.
// Permission to use, copy, modify, and distribute this
// software is freely granted, provided that this notice
// is preserved.
// ====================================================

namespace NanoQL.Numerics;

/// <summary>
/// The error function, erf(x) = (2/√π) ∫₀ˣ exp(−t²) dt, from FDLIBM's <c>erf</c>; it gives the cumulative normal
/// distribution.
/// </summary>
/// <remarks>
/// <para>Method, by |x|:</para>
/// <list type="number">
/// <item><description>[0, 0.84375): erf(x) = x + x·R(x²), R = P/Q, P an odd polynomial of degree 8 and Q of degree
/// 10; |R − (erf(x) − x)/x| ≤ 2^−57.90.</description></item>
/// <item><description>[0.84375, 1.25): with s = |x| − 1 and c = 0.84506291151 rounded to single precision,
/// erf(x) = sign(x)·(c + P1(s)/Q1(s)), P1 and Q1 of degree 6; |P1/Q1 − (erf(|x|) − c)| ≤ 2^−59.06.</description></item>
/// <item><description>[1.25, 1/0.35): erf(x) = 1 − (1/x)·exp(−x² − 0.5625 + R1/S1), R1 of degree 7 and S1 of degree
/// 8 in z = 1/x²; |R1/S1 − f(x)| &lt; 2^−62.57.</description></item>
/// <item><description>[1/0.35, 6): erf(x) = sign(x)·(1 − (1/x)·exp(−x² − 0.5625 + R2/S2)), R2 of degree 6 and S2 of
/// degree 7 in z = 1/x²; |R2/S2 − f(x)| &lt; 2^−61.52.</description></item>
/// <item><description>[6, ∞): erf(x) = sign(x)·(1 − tiny), which is exactly ±1.</description></item>
/// </list>
/// <para>erf(0) = 0, erf(±∞) = ±1, and erf(NaN) is NaN.</para>
/// </remarks>
public sealed class ErrorFunction
{
    // tiny is 1e-300 as in fdlibm: it has to be below half an ulp of 1
    // so that one - tiny, the value returned for |x| >= 6, is exactly 1.
    private const double tiny = 1e-300;
    private const double one = 1.00000000000000000000e+00; /* 0x3FF00000, 0x00000000 */

    /* c = (float)0.84506291151 */
    private const double erx = 8.45062911510467529297e-01; /* 0x3FEB0AC1, 0x60000000 */

    //
    // Coefficients for approximation to  erf on [0,0.84375]
    //
    private const double efx = 1.28379167095512586316e-01; /* 0x3FC06EBA, 0x8214DB69 */
    private const double efx8 = 1.02703333676410069053e+00; /* 0x3FF06EBA, 0x8214DB69 */
    private const double pp0 = 1.28379167095512558561e-01; /* 0x3FC06EBA, 0x8214DB68 */
    private const double pp1 = -3.25042107247001499370e-01; /* 0xBFD4CD7D, 0x691CB913 */
    private const double pp2 = -2.84817495755985104766e-02; /* 0xBF9D2A51, 0xDBD7194F */
    private const double pp3 = -5.77027029648944159157e-03; /* 0xBF77A291, 0x236668E4 */
    private const double pp4 = -2.37630166566501626084e-05; /* 0xBEF8EAD6, 0x120016AC */
    private const double qq1 = 3.97917223959155352819e-01; /* 0x3FD97779, 0xCDDADC09 */
    private const double qq2 = 6.50222499887672944485e-02; /* 0x3FB0A54C, 0x5536CEBA */
    private const double qq3 = 5.08130628187576562776e-03; /* 0x3F74D022, 0xC4D36B0F */
    private const double qq4 = 1.32494738004321644526e-04; /* 0x3F215DC9, 0x221C1A10 */
    private const double qq5 = -3.96022827877536812320e-06; /* 0xBED09C43, 0x42A26120 */

    //
    // Coefficients for approximation to  erf  in [0.84375,1.25]
    //
    private const double pa0 = -2.36211856075265944077e-03; /* 0xBF6359B8, 0xBEF77538 */
    private const double pa1 = 4.14856118683748331666e-01; /* 0x3FDA8D00, 0xAD92B34D */
    private const double pa2 = -3.72207876035701323847e-01; /* 0xBFD7D240, 0xFBB8C3F1 */
    private const double pa3 = 3.18346619901161753674e-01; /* 0x3FD45FCA, 0x805120E4 */
    private const double pa4 = -1.10894694282396677476e-01; /* 0xBFBC6398, 0x3D3E28EC */
    private const double pa5 = 3.54783043256182359371e-02; /* 0x3FA22A36, 0x599795EB */
    private const double pa6 = -2.16637559486879084300e-03; /* 0xBF61BF38, 0x0A96073F */
    private const double qa1 = 1.06420880400844228286e-01; /* 0x3FBB3E66, 0x18EEE323 */
    private const double qa2 = 5.40397917702171048937e-01; /* 0x3FE14AF0, 0x92EB6F33 */
    private const double qa3 = 7.18286544141962662868e-02; /* 0x3FB2635C, 0xD99FE9A7 */
    private const double qa4 = 1.26171219808761642112e-01; /* 0x3FC02660, 0xE763351F */
    private const double qa5 = 1.36370839120290507362e-02; /* 0x3F8BEDC2, 0x6B51DD1C */
    private const double qa6 = 1.19844998467991074170e-02; /* 0x3F888B54, 0x5735151D */

    //
    // Coefficients for approximation to  erfc in [1.25,1/0.35]
    //
    private const double ra0 = -9.86494403484714822705e-03; /* 0xBF843412, 0x600D6435 */
    private const double ra1 = -6.93858572707181764372e-01; /* 0xBFE63416, 0xE4BA7360 */
    private const double ra2 = -1.05586262253232909814e+01; /* 0xC0251E04, 0x41B0E726 */
    private const double ra3 = -6.23753324503260060396e+01; /* 0xC04F300A, 0xE4CBA38D */
    private const double ra4 = -1.62396669462573470355e+02; /* 0xC0644CB1, 0x84282266 */
    private const double ra5 = -1.84605092906711035994e+02; /* 0xC067135C, 0xEBCCABB2 */
    private const double ra6 = -8.12874355063065934246e+01; /* 0xC0545265, 0x57E4D2F2 */
    private const double ra7 = -9.81432934416914548592e+00; /* 0xC023A0EF, 0xC69AC25C */
    private const double sa1 = 1.96512716674392571292e+01; /* 0x4033A6B9, 0xBD707687 */
    private const double sa2 = 1.37657754143519042600e+02; /* 0x4061350C, 0x526AE721 */
    private const double sa3 = 4.34565877475229228821e+02; /* 0x407B290D, 0xD58A1A71 */
    private const double sa4 = 6.45387271733267880336e+02; /* 0x40842B19, 0x21EC2868 */
    private const double sa5 = 4.29008140027567833386e+02; /* 0x407AD021, 0x57700314 */
    private const double sa6 = 1.08635005541779435134e+02; /* 0x405B28A3, 0xEE48AE2C */
    private const double sa7 = 6.57024977031928170135e+00; /* 0x401A47EF, 0x8E484A93 */
    private const double sa8 = -6.04244152148580987438e-02; /* 0xBFAEEFF2, 0xEE749A62 */

    //
    // Coefficients for approximation to  erfc in [1/.35,28]
    //
    private const double rb0 = -9.86494292470009928597e-03; /* 0xBF843412, 0x39E86F4A */
    private const double rb1 = -7.99283237680523006574e-01; /* 0xBFE993BA, 0x70C285DE */
    private const double rb2 = -1.77579549177547519889e+01; /* 0xC031C209, 0x555F995A */
    private const double rb3 = -1.60636384855821916062e+02; /* 0xC064145D, 0x43C5ED98 */
    private const double rb4 = -6.37566443368389627722e+02; /* 0xC083EC88, 0x1375F228 */
    private const double rb5 = -1.02509513161107724954e+03; /* 0xC0900461, 0x6A2E5992 */
    private const double rb6 = -4.83519191608651397019e+02; /* 0xC07E384E, 0x9BDC383F */
    private const double sb1 = 3.03380607434824582924e+01; /* 0x403E568B, 0x261D5190 */
    private const double sb2 = 3.25792512996573918826e+02; /* 0x40745CAE, 0x221B9F0A */
    private const double sb3 = 1.53672958608443695994e+03; /* 0x409802EB, 0x189D5118 */
    private const double sb4 = 3.19985821950859553908e+03; /* 0x40A8FFB7, 0x688C246A */
    private const double sb5 = 2.55305040643316442583e+03; /* 0x40A3F219, 0xCEDF3BE6 */
    private const double sb6 = 4.74528541206955367215e+02; /* 0x407DA874, 0xE79FE763 */
    private const double sb7 = -2.24409524465858183362e+01; /* 0xC03670E2, 0x42712D62 */

    // <cfloat>'s DBL_MIN, the smallest normal double.
    private const double DBL_MIN = 2.2250738585072014E-308;

    /// <summary>The error function at <paramref name="x"/>.</summary>
    /// <param name="x">The argument.</param>
    /// <returns>erf(x), in [−1, 1]; NaN for NaN.</returns>
    public double Evaluate(double x)
    {
        double R, S, P, Q, s, y, z, r, ax;

        if (!double.IsFinite(x))
        {
            if (double.IsNaN(x))
            {
                return x;
            }
            else
            {
                return x > 0 ? 1 : -1;
            }
        }

        ax = Math.Abs(x);

        if (ax < 0.84375)
        {
            /* |x|<0.84375 */
            if (ax < 3.7252902984e-09)
            {
                /* |x|<2**-28 */
                if (ax < DBL_MIN * 16)
                {
                    return 0.125 * (8.0 * x + efx8 * x); /*avoid underflow */
                }

                return x + efx * x;
            }

            z = x * x;
            r = pp0 + z * (pp1 + z * (pp2 + z * (pp3 + z * pp4)));
            s = one + z * (qq1 + z * (qq2 + z * (qq3 + z * (qq4 + z * qq5))));
            y = r / s;
            return x + x * y;
        }

        if (ax < 1.25)
        {
            /* 0.84375 <= |x| < 1.25 */
            s = ax - one;
            P = pa0 + s * (pa1 + s * (pa2 + s * (pa3 + s * (pa4 + s * (pa5 + s * pa6)))));
            Q = one + s * (qa1 + s * (qa2 + s * (qa3 + s * (qa4 + s * (qa5 + s * qa6)))));
            if (x >= 0)
            {
                return erx + P / Q;
            }
            else
            {
                return -erx - P / Q;
            }
        }

        if (ax >= 6)
        {
            /* inf>|x|>=6 */
            if (x >= 0)
            {
                return one - tiny;
            }
            else
            {
                return tiny - one;
            }
        }

        /* Starts to lose accuracy when ax~5 */
        s = one / (ax * ax);

        if (ax < 2.85714285714285)
        {
            /* |x| < 1/0.35 */
            R = ra0 + s * (ra1 + s * (ra2 + s * (ra3 + s * (ra4 + s * (ra5 + s * (ra6 + s * ra7))))));
            S = one + s * (sa1 + s * (sa2 + s * (sa3 + s * (sa4 + s * (sa5 + s * (sa6 + s * (sa7 + s * sa8)))))));
        }
        else
        {
            /* |x| >= 1/0.35 */
            R = rb0 + s * (rb1 + s * (rb2 + s * (rb3 + s * (rb4 + s * (rb5 + s * rb6)))));
            S = one + s * (sb1 + s * (sb2 + s * (sb3 + s * (sb4 + s * (sb5 + s * (sb6 + s * sb7))))));
        }

        r = Math.Exp(-ax * ax - 0.5625 + R / S);
        if (x >= 0)
        {
            return one - r / ax;
        }
        else
        {
            return r / ax - one;
        }
    }
}
