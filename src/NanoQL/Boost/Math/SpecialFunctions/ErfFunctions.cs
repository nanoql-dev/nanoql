// SPDX-FileCopyrightText: (C) Copyright John Maddock 2006.
// SPDX-FileCopyrightText: (C) Copyright Matt Borland 2024.
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSL-1.0
// Upstream: boost/math/special_functions/erf.hpp @ boost-1.90.0
// Ported to C# from Boost and modified by the nanoql Authors.

using System.Globalization;
using static NanoQL.Numerics.PolynomialHorner2Functions;

namespace NanoQL.Numerics;

/// <summary>
/// Boost.Math's error functions (<c>erf.hpp</c>) for <c>double</c>: <c>erf</c>, <c>erfc</c> and the 53-bit branch of
/// <c>erf_imp</c>, which Boost's precision policy selects for <c>double</c>. Boost evaluates in <c>long double</c>,
/// which under MSVC is <c>double</c>, so <c>exp</c> is <see cref="Math.Exp"/>. Their <c>checked_narrowing_cast</c>
/// isn't ported: its overflow check can't fire, since the results lie in [−1, 2].
/// </summary>
internal static class ErfFunctions
{
    // z < 1e-10: erf(z) is z·(1.125 + c), 2/√π split so that its first term is exact.
    private const double C = 0.003379167095512573896158903121545171688;

    // [1e-10, 0.5): erf(z) = z·(Y + P(z²)/Q(z²)). Maximum deviation found 1.561e-17.
    private const double Y1 = 1.044948577880859375f;

    // [0.5, 1.5): erfc(z) = (Y + P(z − 0.5)/Q(z − 0.5))·exp(−z²)/z. Maximum deviation found 3.702e-17.
    private const double Y2 = 0.405935764312744140625f;

    // [1.5, 2.5): erfc(z) = (Y + P(z − 1.5)/Q(z − 1.5))·exp(−z²)/z. Maximum deviation found 3.909e-18.
    private const double Y3 = 0.50672817230224609375f;

    // [2.5, 4.5): erfc(z) = (Y + P(z − 3.5)/Q(z − 3.5))·exp(−z²)/z. Maximum deviation found 1.512e-17.
    private const double Y4 = 0.5405750274658203125f;

    // [4.5, 28): erfc(z) = (Y + P(1/z)/Q(1/z))·exp(−z²)/z. Maximum deviation found 2.860e-17.
    private const double Y5 = 0.5579090118408203125f;

    private static ReadOnlySpan<double> P1 =>
    [
        0.0834305892146531832907,
        -0.338165134459360935041,
        -0.0509990735146777432841,
        -0.00772758345802133288487,
        -0.000322780120964605683831,
    ];

    private static ReadOnlySpan<double> Q1 =>
    [
        1.0,
        0.455004033050794024546,
        0.0875222600142252549554,
        0.00858571925074406212772,
        0.000370900071787748000569,
    ];

    private static ReadOnlySpan<double> P2 =>
    [
        -0.098090592216281240205,
        0.178114665841120341155,
        0.191003695796775433986,
        0.0888900368967884466578,
        0.0195049001251218801359,
        0.00180424538297014223957,
    ];

    private static ReadOnlySpan<double> Q2 =>
    [
        1.0,
        1.84759070983002217845,
        1.42628004845511324508,
        0.578052804889902404909,
        0.12385097467900864233,
        0.0113385233577001411017,
        0.337511472483094676155e-5,
    ];

    private static ReadOnlySpan<double> P3 =>
    [
        -0.0243500476207698441272,
        0.0386540375035707201728,
        0.04394818964209516296,
        0.0175679436311802092299,
        0.00323962406290842133584,
        0.000235839115596880717416,
    ];

    private static ReadOnlySpan<double> Q3 =>
    [
        1.0,
        1.53991494948552447182,
        0.982403709157920235114,
        0.325732924782444448493,
        0.0563921837420478160373,
        0.00410369723978904575884,
    ];

    private static ReadOnlySpan<double> P4 =>
    [
        0.00295276716530971662634,
        0.0137384425896355332126,
        0.00840807615555585383007,
        0.00212825620914618649141,
        0.000250269961544794627958,
        0.113212406648847561139e-4,
    ];

    private static ReadOnlySpan<double> Q4 =>
    [
        1.0,
        1.04217814166938418171,
        0.442597659481563127003,
        0.0958492726301061423444,
        0.0105982906484876531489,
        0.000479411269521714493907,
    ];

    private static ReadOnlySpan<double> P5 =>
    [
        0.00628057170626964891937,
        0.0175389834052493308818,
        -0.212652252872804219852,
        -0.687717681153649930619,
        -2.5518551727311523996,
        -3.22729451764143718517,
        -2.8175401114513378771,
    ];

    private static ReadOnlySpan<double> Q5 =>
    [
        1.0,
        2.79257750980575282228,
        11.0567237927800161565,
        15.930646027911794143,
        22.9367376522880577224,
        13.5064170191802889145,
        5.48409182238641741584,
    ];

    /// <summary><c>erf(z)</c>: the error function.</summary>
    /// <param name="z">The argument.</param>
    /// <returns>erf(z), in [−1, 1].</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="z"/> is NaN.</exception>
    internal static double Erf(double z) => ErfImp(z, false);

    /// <summary><c>erfc(z)</c>: the complementary error function, 1 − erf(z).</summary>
    /// <param name="z">The argument.</param>
    /// <returns>erfc(z), in [0, 2].</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="z"/> is NaN.</exception>
    internal static double Erfc(double z) => ErfImp(z, true);

    // erf_imp for the 53-bit tag: erf(z), or erfc(z) when invert is set.
    private static double ErfImp(double z, bool invert)
    {
        if (double.IsNaN(z))
        {
            throw new ArgumentOutOfRangeException(
                nameof(z),
                "Error in function boost::math::erf<double>(double): Expected a finite argument but got "
                + z.ToString(CultureInfo.InvariantCulture));
        }

        int prefix_multiplier = 1;
        int prefix_adder = 0;

        if (z < 0)
        {
            // Recursion is logically simpler here, but confuses static analyzers that need to be
            // able to calculate the maximimum program stack size at compile time (ie CUDA).
            z = -z;
            if (!invert)
            {
                prefix_multiplier = -1;
                // return -erf_imp(T(-z), invert, pol, t);
            }
            else if (z > 0.5)
            {
                prefix_adder = 2;
                prefix_multiplier = -1;
                // return 2 - erf_imp(T(-z), invert, pol, t);
            }
            else
            {
                invert = false;
                prefix_adder = 1;
                // return 1 + erf_imp(T(-z), false, pol, t);
            }
        }

        double result;

        //
        // Big bunch of selection statements now to pick
        // which implementation to use,
        // try to put most likely options first:
        //
        if (z < 0.5)
        {
            //
            // We're going to calculate erf:
            //
            if (z < 1e-10)
            {
                if (z == 0)
                {
                    result = 0;
                }
                else
                {
                    result = z * 1.125f + z * C;
                }
            }
            else
            {
                double zz = z * z;
                result = z * (Y1 + EvaluatePolynomial(P1, zz) / EvaluatePolynomial(Q1, zz));
            }
        }
        else if (invert ? (z < 28) : (z < 5.93f))
        {
            //
            // We'll be calculating erfc:
            //
            invert = !invert;
            if (z < 1.5f)
            {
                result = Y2 + EvaluatePolynomial(P2, z - 0.5) / EvaluatePolynomial(Q2, z - 0.5);
                result *= Math.Exp(-z * z) / z;
            }
            else if (z < 2.5f)
            {
                result = Y3 + EvaluatePolynomial(P3, z - 1.5) / EvaluatePolynomial(Q3, z - 1.5);
                result *= ExpOfMinusSquareOver(z);
            }
            else if (z < 4.5f)
            {
                result = Y4 + EvaluatePolynomial(P4, z - 3.5) / EvaluatePolynomial(Q4, z - 3.5);
                result *= ExpOfMinusSquareOver(z);
            }
            else
            {
                result = Y5 + EvaluatePolynomial(P5, 1 / z) / EvaluatePolynomial(Q5, 1 / z);
                result *= ExpOfMinusSquareOver(z);
            }
        }
        else
        {
            //
            // Any value of z larger than 28 will underflow to zero:
            //
            result = 0;
            invert = !invert;
        }

        if (invert)
        {
            prefix_adder += prefix_multiplier * 1;
            prefix_multiplier = -prefix_multiplier;
        }

        return prefix_adder + prefix_multiplier * result;
    }

    // exp(-z*z)/z with z*z split into a high part of 26 bits and the rest, so that exp's argument carries no rounding
    // error: erf.hpp writes these lines out in each of its three branches from 1.5 up.
    private static double ExpOfMinusSquareOver(double z)
    {
        double hi, lo;
        int expon;
        hi = Math.Floor(Math.ScaleB(Frexp(z, out expon), 26));
        hi = Math.ScaleB(hi, expon - 26);
        lo = z - hi;
        double sq = z * z;
        double err_sqr = ((hi * hi - sq) + 2 * hi * lo) + lo * lo;
        return Math.Exp(-sq) * Math.Exp(-err_sqr) / z;
    }

    // std::frexp for a finite, nonzero x: the mantissa in [0.5, 1) and the exponent, x = mantissa·2^exponent.
    private static double Frexp(double x, out int exponent)
    {
        exponent = Math.ILogB(x) + 1;
        return Math.ScaleB(x, -exponent);
    }
}
