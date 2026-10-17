// SPDX-FileCopyrightText: Copyright (C) 2002, 2003 Ferdinando Ametrano
// SPDX-FileCopyrightText: Copyright (C) 2000, 2001, 2002, 2003 RiskMap srl
// SPDX-FileCopyrightText: Copyright (C) 2010 Kakhkhor Abdijalilov
// SPDX-FileCopyrightText: Copyright (C) 2008 StatPro Italia srl
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/math/distributions/normaldistribution.hpp ql/math/distributions/normaldistribution.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using System.Globalization;

namespace NanoQL.Numerics;

/// <summary>
/// The inverse cumulative normal distribution: given a probability p between zero and one, the y with F(y) = p, F the
/// cumulative normal distribution of average μ and standard deviation σ. It uses Acklam's approximation, by Peter J.
/// Acklam, University of Oslo, Statistics Division.
/// </summary>
/// <remarks>
/// <para>
/// It also turns a uniform distribution into a normal one; for a low-discrepancy uniform sequence it keeps the
/// sequence's low discrepancy, which the Box–Muller approach and its variants don't.
/// </para>
/// <para>
/// A configuration struct: its zero value (<c>default</c>, <c>new InverseCumulativeNormal()</c>, an unassigned field
/// or array element) is QuantLib's default construction, the standard normal (μ = 0, σ = 1). QuantLib's static
/// <c>f_</c>, a cumulative distribution that only its disabled Halley refinement reads, isn't ported, nor its alias
/// <c>InvCumulativeNormalDistribution</c>.
/// </para>
/// </remarks>
public readonly struct InverseCumulativeNormal : IInverseCumulative
{
    // Coefficients for the rational approximation.
    private const double a1_ = -3.969683028665376e+01;
    private const double a2_ = 2.209460984245205e+02;
    private const double a3_ = -2.759285104469687e+02;
    private const double a4_ = 1.383577518672690e+02;
    private const double a5_ = -3.066479806614716e+01;
    private const double a6_ = 2.506628277459239e+00;

    private const double b1_ = -5.447609879822406e+01;
    private const double b2_ = 1.615858368580409e+02;
    private const double b3_ = -1.556989798598866e+02;
    private const double b4_ = 6.680131188771972e+01;
    private const double b5_ = -1.328068155288572e+01;

    private const double c1_ = -7.784894002430293e-03;
    private const double c2_ = -3.223964580411365e-01;
    private const double c3_ = -2.400758277161838e+00;
    private const double c4_ = -2.549732539343734e+00;
    private const double c5_ = 4.374664141464968e+00;
    private const double c6_ = 2.938163982698783e+00;

    private const double d1_ = 7.784695709041462e-03;
    private const double d2_ = 3.224671290700398e-01;
    private const double d3_ = 2.445134137142996e+00;
    private const double d4_ = 3.754408661907416e+00;

    // Limits of the approximation regions
    private const double x_low_ = 0.02425;
    private const double x_high_ = 1.0 - x_low_;

    private readonly double average;

    // QuantLib's sigma. The constructor refuses 0, so a stored 0 is the zero value, which reads as QuantLib's
    // default, 1.
    private readonly double sigma;

    /// <summary>Initializes a new instance of the <see cref="InverseCumulativeNormal"/> struct.</summary>
    /// <param name="average">The average μ.</param>
    /// <param name="sigma">The standard deviation σ, positive.</param>
    /// <exception cref="ArgumentException"><paramref name="sigma"/> isn't positive.</exception>
    public InverseCumulativeNormal(double average = 0.0, double sigma = 1.0)
    {
        if (!(sigma > 0.0))
        {
            throw new ArgumentException(
                $"sigma must be greater than 0.0 ({sigma.ToString(CultureInfo.InvariantCulture)} not allowed)",
                nameof(sigma));
        }

        this.average = average;
        this.sigma = sigma;
    }

    private double Sigma => sigma == 0.0 ? 1.0 : sigma;

    /// <summary>The inverse at <paramref name="x"/>, μ + σ·<see cref="StandardValue"/>(x).</summary>
    /// <param name="x">The probability, in (0, 1); 0 and 1 within rounding give the most negative and the largest double.</param>
    /// <returns>The inverse.</returns>
    /// <exception cref="NanoQLException"><paramref name="x"/> is outside [0, 1] beyond rounding.</exception>
    public double Evaluate(double x) => average + Sigma * StandardValue(x);

    /// <summary>
    /// The inverse of the standard normal at <paramref name="x"/>, for average 0 and standard deviation 1, without the
    /// two floating-point operations of <see cref="Evaluate"/>.
    /// </summary>
    /// <param name="x">The probability, in (0, 1); 0 and 1 within rounding give the most negative and the largest double.</param>
    /// <returns>The inverse.</returns>
    /// <exception cref="NanoQLException"><paramref name="x"/> is outside [0, 1] beyond rounding.</exception>
    public static double StandardValue(double x)
    {
        double z;
        if (x < x_low_ || x_high_ < x)
        {
            z = TailValue(x);
        }
        else
        {
            z = x - 0.5;
            double r = z * z;
            z = (((((a1_ * r + a2_) * r + a3_) * r + a4_) * r + a5_) * r + a6_) * z /
                (((((b1_ * r + b2_) * r + b3_) * r + b4_) * r + b5_) * r + 1.0);
        }

        // The relative error of the approximation has absolute value less
        // than 1.15e-9.  One iteration of Halley's rational method (third
        // order) would give full machine precision; QuantLib leaves it disabled
        // (REFINE_TO_FULL_MACHINE_PRECISION_USING_HALLEYS_METHOD).
        return z;
    }

    // Handling tails moved into a separate method, which should make the inlining of operator() and standard_value
    // easier. tail_value is called rarely and doesn't need to be inlined.
    private static double TailValue(double x)
    {
        if (x <= 0.0 || x >= 1.0)
        {
            // try to recover if due to numerical error
            if (ComparisonFunctions.CloseEnough(x, 1.0))
            {
                return NumericLimits.QL_MAX_REAL; // largest value available
            }
            else if (Math.Abs(x) < NumericLimits.QL_EPSILON)
            {
                return NumericLimits.QL_MIN_REAL; // largest negative value available
            }
            else
            {
                throw new NanoQLException(
                    $"InverseCumulativeNormal({x.ToString(CultureInfo.InvariantCulture)}) undefined: must be 0 < x < 1");
            }
        }

        double z;
        if (x < x_low_)
        {
            // Rational approximation for the lower region 0<x<u_low
            z = Math.Sqrt(-2.0 * Math.Log(x));
            z = (((((c1_ * z + c2_) * z + c3_) * z + c4_) * z + c5_) * z + c6_) /
                ((((d1_ * z + d2_) * z + d3_) * z + d4_) * z + 1.0);
        }
        else
        {
            // Rational approximation for the upper region u_high<x<1
            z = Math.Sqrt(-2.0 * Math.Log(1.0 - x));
            z = -(((((c1_ * z + c2_) * z + c3_) * z + c4_) * z + c5_) * z + c6_) /
                ((((d1_ * z + d2_) * z + d3_) * z + d4_) * z + 1.0);
        }

        return z;
    }
}
