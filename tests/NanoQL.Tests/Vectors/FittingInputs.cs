// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.TermStructures;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// Builds the fitting methods the fitted-curve vector files describe, as
/// <c>tools/vectors/gen/termstructures/yield/nonlinearfittingmethods.py</c>'s <c>make_method</c> builds them.
/// </summary>
/// <remarks>
/// A method is <c>{"type": "ExponentialSplines", "constrainAtZero", "numCoeffs"}</c>, <c>{"type": "NelsonSiegel"}</c>,
/// <c>{"type": "Svensson"}</c>, <c>{"type": "CubicBSplines", "knots", "constrainAtZero"}</c>, <c>{"type":
/// "SimplePolynomial", "degree", "constrainAtZero"}</c> or <c>{"type": "Spread", "method", "discountCurve"}</c>, the
/// discounting curve <see cref="YieldCurveInputs.OriginalOf"/>'s spec; each with optional <c>"minCutoffTime"</c> and
/// <c>"maxCutoffTime"</c>, QuantLib's 0 and <c>QL_MAX_REAL</c> when absent.
/// </remarks>
internal static class FittingInputs
{
    /// <summary>The method a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's context, which a spread method's discounting curve is built in.</param>
    /// <returns>The method.</returns>
    public static FittedBondDiscountCurveFittingMethod MethodOf(JsonElement spec, EvaluationContext context)
    {
        double lo = spec.TryGetProperty("minCutoffTime", out JsonElement min) ? min.GetDouble() : 0.0;
        double hi = spec.TryGetProperty("maxCutoffTime", out JsonElement max) ? max.GetDouble() : double.MaxValue;
        return spec.GetProperty("type").GetString() switch
        {
            "ExponentialSplines" => new ExponentialSplinesFitting(
                spec.GetProperty("constrainAtZero").GetBoolean(),
                minCutoffTime: lo,
                maxCutoffTime: hi,
                numCoeffs: spec.GetProperty("numCoeffs").GetInt32()),
            "NelsonSiegel" => new NelsonSiegelFitting(minCutoffTime: lo, maxCutoffTime: hi),
            "Svensson" => new SvenssonFitting(minCutoffTime: lo, maxCutoffTime: hi),
            "CubicBSplines" => new CubicBSplinesFitting(
                spec.GetProperty("knots").EnumerateArray().Select(k => k.GetDouble()),
                spec.GetProperty("constrainAtZero").GetBoolean(),
                minCutoffTime: lo,
                maxCutoffTime: hi),
            "SimplePolynomial" => new SimplePolynomialFitting(
                spec.GetProperty("degree").GetInt32(),
                spec.GetProperty("constrainAtZero").GetBoolean(),
                minCutoffTime: lo,
                maxCutoffTime: hi),
            "Spread" => new SpreadFittingMethod(
                MethodOf(spec.GetProperty("method"), context),
                YieldCurveInputs.OriginalOf(spec.GetProperty("discountCurve"), context),
                lo,
                hi),
            var t => throw new InvalidOperationException($"unknown fitting method '{t}'"),
        };
    }
}
