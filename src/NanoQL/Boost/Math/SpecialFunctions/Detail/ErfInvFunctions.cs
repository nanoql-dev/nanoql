// SPDX-FileCopyrightText: (C) Copyright John Maddock 2006.
// SPDX-FileCopyrightText: (C) Copyright Matt Borland 2024.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/math/special_functions/detail/erf_inv.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

using System.Globalization;
using static NanoQL.Numerics.PolynomialHorner2Functions;

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Math's inverse complementary error function (<c>erf_inv.hpp</c>) for <c>double</c>: <c>erfc_inv</c> and
/// <c>erf_inv_imp</c>'s branch for the 64-bit tag, which Boost's precision policy selects for <c>double</c> (53 bits)
/// with no Halley refinement. <c>log</c> and <c>sqrt</c> are <see cref="Math.Log(double)"/> and
/// <see cref="Math.Sqrt"/>.
/// <c>erfc_inv</c>'s <c>checked_narrowing_cast</c> isn't ported: its overflow check can't fire, since the result is
/// finite for every argument that reaches it.
/// </summary>
internal static class ErfInvFunctions
{
    // p <= 0.5: x = p(p+10)(Y+R(p)). double: max error found 2.001849e-18.
    private const float Y1 = 0.0891314744949340820313f;

    // 0.5 > q >= 0.25: x = sqrt(-2*log(q)) / (Y + R(q)). double: max error found 7.403372e-17.
    private const float Y2 = 2.249481201171875f;

    // q < 0.25, with x = sqrt(-log(q)): the result is x(Y+R(x-B)), B the lowest x for which each form is valid.
    private const float Y3 = 0.807220458984375f;
    private const float Y4 = 0.93995571136474609375f;
    private const float Y5 = 0.98362827301025390625f;
    private const float Y6 = 0.99714565277099609375f;
    private const float Y7 = 0.99941349029541015625f;

    private static ReadOnlySpan<double> P1 =>
    [
        -0.000508781949658280665617,
        -0.00836874819741736770379,
        0.0334806625409744615033,
        -0.0126926147662974029034,
        -0.0365637971411762664006,
        0.0219878681111168899165,
        0.00822687874676915743155,
        -0.00538772965071242932965,
    ];

    private static ReadOnlySpan<double> Q1 =>
    [
        1.0,
        -0.970005043303290640362,
        -1.56574558234175846809,
        1.56221558398423026363,
        0.662328840472002992063,
        -0.71228902341542847553,
        -0.0527396382340099713954,
        0.0795283687341571680018,
        -0.00233393759374190016776,
        0.000886216390456424707504,
    ];

    private static ReadOnlySpan<double> P2 =>
    [
        -0.202433508355938759655,
        0.105264680699391713268,
        8.37050328343119927838,
        17.6447298408374015486,
        -18.8510648058714251895,
        -44.6382324441786960818,
        17.445385985570866523,
        21.1294655448340526258,
        -3.67192254707729348546,
    ];

    private static ReadOnlySpan<double> Q2 =>
    [
        1.0,
        6.24264124854247537712,
        3.9713437953343869095,
        -28.6608180499800029974,
        -20.1432634680485188801,
        48.5609213108739935468,
        10.8268667355460159008,
        -22.6436933413139721736,
        1.72114765761200282724,
    ];

    private static ReadOnlySpan<double> P3 =>
    [
        -0.131102781679951906451,
        -0.163794047193317060787,
        0.117030156341995252019,
        0.387079738972604337464,
        0.337785538912035898924,
        0.142869534408157156766,
        0.0290157910005329060432,
        0.00214558995388805277169,
        -0.679465575181126350155e-6,
        0.285225331782217055858e-7,
        -0.681149956853776992068e-9,
    ];

    private static ReadOnlySpan<double> Q3 =>
    [
        1.0,
        3.46625407242567245975,
        5.38168345707006855425,
        4.77846592945843778382,
        2.59301921623620271374,
        0.848854343457902036425,
        0.152264338295331783612,
        0.01105924229346489121,
    ];

    private static ReadOnlySpan<double> P4 =>
    [
        -0.0350353787183177984712,
        -0.00222426529213447927281,
        0.0185573306514231072324,
        0.00950804701325919603619,
        0.00187123492819559223345,
        0.000157544617424960554631,
        0.460469890584317994083e-5,
        -0.230404776911882601748e-9,
        0.266339227425782031962e-11,
    ];

    private static ReadOnlySpan<double> Q4 =>
    [
        1.0,
        1.3653349817554063097,
        0.762059164553623404043,
        0.220091105764131249824,
        0.0341589143670947727934,
        0.00263861676657015992959,
        0.764675292302794483503e-4,
    ];

    private static ReadOnlySpan<double> P5 =>
    [
        -0.0167431005076633737133,
        -0.00112951438745580278863,
        0.00105628862152492910091,
        0.000209386317487588078668,
        0.149624783758342370182e-4,
        0.449696789927706453732e-6,
        0.462596163522878599135e-8,
        -0.281128735628831791805e-13,
        0.99055709973310326855e-16,
    ];

    private static ReadOnlySpan<double> Q5 =>
    [
        1.0,
        0.591429344886417493481,
        0.138151865749083321638,
        0.0160746087093676504695,
        0.000964011807005165528527,
        0.275335474764726041141e-4,
        0.282243172016108031869e-6,
    ];

    private static ReadOnlySpan<double> P6 =>
    [
        -0.0024978212791898131227,
        -0.779190719229053954292e-5,
        0.254723037413027451751e-4,
        0.162397777342510920873e-5,
        0.396341011304801168516e-7,
        0.411632831190944208473e-9,
        0.145596286718675035587e-11,
        -0.116765012397184275695e-17,
    ];

    private static ReadOnlySpan<double> Q6 =>
    [
        1.0,
        0.207123112214422517181,
        0.0169410838120975906478,
        0.000690538265622684595676,
        0.145007359818232637924e-4,
        0.144437756628144157666e-6,
        0.509761276599778486139e-9,
    ];

    private static ReadOnlySpan<double> P7 =>
    [
        -0.000539042911019078575891,
        -0.28398759004727721098e-6,
        0.899465114892291446442e-6,
        0.229345859265920864296e-7,
        0.225561444863500149219e-9,
        0.947846627503022684216e-12,
        0.135880130108924861008e-14,
        -0.348890393399948882918e-21,
    ];

    private static ReadOnlySpan<double> Q7 =>
    [
        1.0,
        0.0845746234001899436914,
        0.00282092984726264681981,
        0.468292921940894236786e-4,
        0.399968812193862100054e-6,
        0.161809290887904476097e-8,
        0.231558608310259605225e-11,
    ];

    /// <summary><c>erfc_inv(z)</c>: the x with erfc(x) = z.</summary>
    /// <param name="z">The argument, in [0, 2].</param>
    /// <returns>The inverse.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="z"/> is outside [0, 2].</exception>
    /// <exception cref="NanoQLException"><paramref name="z"/> is 0 or 2, whose inverse overflows.</exception>
    internal static double ErfcInv(double z)
    {
        const string function = "boost::math::erfc_inv<double>(double, double)";

        //
        // Begin by testing for domain errors, and other special cases:
        //
        if ((z < 0) || (z > 2))
        {
            throw new ArgumentOutOfRangeException(
                nameof(z),
                $"Error in function {function}: Argument outside range [0,2] in inverse erfc function (got p="
                + z.ToString(CultureInfo.InvariantCulture) + ").");
        }

        if (z == 0)
        {
            throw new NanoQLException($"Error in function {function}: Overflow Error");
        }

        if (z == 2)
        {
            throw new NanoQLException($"Error in function {function}: Overflow Error");
        }

        //
        // Normalise the input, so it's in the range [0,1], we will
        // negate the result if z is outside that range.  This is a simple
        // application of the erfc reflection formula: erfc(-z) = 2 - erfc(z)
        //
        double p, q, s;
        if (z > 1)
        {
            q = 2 - z;
            p = 1 - q;
            s = -1;
        }
        else
        {
            p = 1 - z;
            q = z;
            s = 1;
        }

        return s * ErfInvImp(p, q);
    }

    // erf_inv_imp for the 64-bit tag: the inverse of erf at p, with q = 1 - p.
    private static double ErfInvImp(double p, double q)
    {
        double result = 0;

        if (p <= 0.5)
        {
            //
            // Evaluate inverse erf using the rational approximation:
            //
            // x = p(p+10)(Y+R(p))
            //
            // Where Y is a constant, and R(p) is optimised for a low
            // absolute error compared to |Y|.
            //
            double g = p * (p + 10);
            double r = EvaluatePolynomial(P1, p) / EvaluatePolynomial(Q1, p);
            result = g * Y1 + g * r;
        }
        else if (q >= 0.25)
        {
            //
            // Rational approximation for 0.5 > q >= 0.25
            //
            // x = sqrt(-2*log(q)) / (Y + R(q))
            //
            // Where Y is a constant, and R(q) is optimised for a low
            // absolute error compared to Y.
            //
            double g = Math.Sqrt(-2 * Math.Log(q));
            double xs = q - 0.25f;
            double r = EvaluatePolynomial(P2, xs) / EvaluatePolynomial(Q2, xs);
            result = g / (Y2 + r);
        }
        else
        {
            //
            // For q < 0.25 we have a series of rational approximations all
            // of the general form:
            //
            // let: x = sqrt(-log(q))
            //
            // Then the result is given by:
            //
            // x(Y+R(x-B))
            //
            // where Y is a constant, B is the lowest value of x for which
            // the approximation is valid, and R(x-B) is optimised for a low
            // absolute error compared to Y.
            //
            double x = Math.Sqrt(-Math.Log(q));
            if (x < 3)
            {
                double xs = x - 1.125f;
                double R = EvaluatePolynomial(P3, xs) / EvaluatePolynomial(Q3, xs);
                result = Y3 * x + R * x;
            }
            else if (x < 6)
            {
                double xs = x - 3;
                double R = EvaluatePolynomial(P4, xs) / EvaluatePolynomial(Q4, xs);
                result = Y4 * x + R * x;
            }
            else if (x < 18)
            {
                double xs = x - 6;
                double R = EvaluatePolynomial(P5, xs) / EvaluatePolynomial(Q5, xs);
                result = Y5 * x + R * x;
            }
            else if (x < 44)
            {
                double xs = x - 18;
                double R = EvaluatePolynomial(P6, xs) / EvaluatePolynomial(Q6, xs);
                result = Y6 * x + R * x;
            }
            else
            {
                // Unreachable for a double: the smallest positive q, 4.9e-324, gives x = 27.3. Boost reaches this
                // form only for wider types, and it is ported with the rest of the branch.
                double xs = x - 44;
                double R = EvaluatePolynomial(P7, xs) / EvaluatePolynomial(Q7, xs);
                result = Y7 * x + R * x;
            }
        }

        return result;
    }
}
