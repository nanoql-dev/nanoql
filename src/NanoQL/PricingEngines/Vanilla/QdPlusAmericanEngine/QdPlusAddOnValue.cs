// SPDX-FileCopyrightText: Copyright (C) 2022 Klaus Spanderen
// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0 AND BSD-3-Clause
// Upstream: ql/pricingengines/vanilla/qdplusamericanengine.hpp ql/pricingengines/vanilla/qdplusamericanengine.cpp @ v1.44
// Ported to C# from QuantLib and modified by the nanoql Authors.

using NanoQL.Numerics;

namespace NanoQL.PricingEngines;

/// <summary>
/// The early-exercise premium's integrand of the QD engines, in z = √t: QuantLib's <c>detail::QdPlusAddOnValue</c>,
/// the exercise boundary b(t) = x_max·exp(−√q(z)) read from its Chebyshev interpolation q(z).
/// </summary>
internal sealed class QdPlusAddOnValue
{
    private readonly double bigT;
    private readonly double s;
    private readonly double k;
    private readonly double xmax;
    private readonly double r;
    private readonly double q;
    private readonly double vol;
    private readonly Interpolation qZ;
    private readonly CumulativeNormalDistribution phi = new();

    /// <summary>Initializes a new instance of the <see cref="QdPlusAddOnValue"/> class.</summary>
    /// <param name="t">The time to expiry.</param>
    /// <param name="s">The spot.</param>
    /// <param name="k">The strike.</param>
    /// <param name="r">The risk-free rate.</param>
    /// <param name="q">The dividend yield.</param>
    /// <param name="vol">The volatility.</param>
    /// <param name="xmax">The boundary's limit at expiry.</param>
    /// <param name="qZ">The boundary's interpolation.</param>
    internal QdPlusAddOnValue(double t, double s, double k, double r, double q, double vol, double xmax, Interpolation qZ)
    {
        bigT = t;
        this.s = s;
        this.k = k;
        this.xmax = xmax;
        this.r = r;
        this.q = q;
        this.vol = vol;
        this.qZ = qZ;
    }

    /// <summary>The integrand at z.</summary>
    /// <param name="z">The square root of the time.</param>
    /// <returns>The integrand.</returns>
    internal double Evaluate(double z)
    {
        double t = z * z;
        double qValue = qZ.Evaluate((2 * Math.Sqrt(Max(0.0, bigT - t) / bigT)) - 1, true);
        double bT = xmax * Math.Exp(-Math.Sqrt(Max(0.0, qValue)));

        double dr = Math.Exp(-r * t);
        double dq = Math.Exp(-q * t);
        double v = vol * Math.Sqrt(t);

        double result;
        if (v >= NumericLimits.QL_EPSILON)
        {
            if (bT > NumericLimits.QL_EPSILON)
            {
                double dp = (Math.Log(s * dq / (bT * dr)) / v) + (0.5 * v);
                result = 2 * z * ((r * k * dr * phi.Evaluate(-dp + v)) - (q * s * dq * phi.Evaluate(-dp)));
            }
            else
            {
                result = 0.0;
            }
        }
        else if (ComparisonFunctions.CloseEnough(s * dq, bT * dr))
        {
            result = z * ((r * k * dr) - (q * s * dq));
        }
        else if (bT * dr > s * dq)
        {
            result = 2 * z * ((r * k * dr) - (q * s * dq));
        }
        else
        {
            result = 0.0;
        }

        return result;
    }

    // std::max's semantics, which keep or drop a NaN by position where Math.Max propagates it.
    private static double Max(double a, double b) => a < b ? b : a;
}
