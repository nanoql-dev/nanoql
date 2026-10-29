// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// Readers of the C++ harness's basis model specs, as <c>tools/cxx/src/experimental/basismodels/basismodels.cpp</c>
/// reads them: test-suite/basismodels.cpp's curves, indexes and base structures.
/// </summary>
internal static class BasisModelInputs
{
    /// <summary>A cubic zero curve on Actual/365 (Fixed) and NullCalendar: the test's <c>getYTS</c>.</summary>
    /// <param name="spec">The curve spec, its dates and rates.</param>
    /// <returns>The curve.</returns>
    public static YieldTermStructure CurveOf(JsonElement spec) =>
        new InterpolatedZeroCurve<Cubic>(
            spec.GetProperty("dates").EnumerateArray().Select(d => new Date(d.GetInt32())),
            spec.GetProperty("rates").EnumerateArray().Select(x => x.GetDouble()),
            new Actual365Fixed(),
            new NullCalendar());

    /// <summary>Euribor 3M or 6M on a curve.</summary>
    /// <param name="spec">The index spec, its tenor and curve.</param>
    /// <returns>The index.</returns>
    public static IborIndex IndexOf(JsonElement spec)
    {
        YieldTermStructure curve = CurveOf(spec.GetProperty("curve"));
        return spec.GetProperty("tenor").GetString() == "3M" ? new Euribor3M(curve) : new Euribor6M(curve);
    }

    /// <summary>The test's normal caplet volatilities on Euribor 3M, adapted: its <c>getOptionletTS</c>.</summary>
    /// <param name="spec">The base spec, its curve, dates, strikes and volatilities.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The structure.</returns>
    public static OptionletVolatilityStructure OptionletOf(JsonElement spec, EvaluationContext context)
    {
        var stripped = new StrippedOptionlet(
            2,
            new TARGET(),
            BusinessDayConvention.Following,
            new Euribor3M(CurveOf(spec.GetProperty("curve"))),
            spec.GetProperty("dates").EnumerateArray().Select(d => new Date(d.GetInt32())),
            spec.GetProperty("strikes").EnumerateArray().Select(x => x.GetDouble()),
            spec.GetProperty("volatilities").EnumerateArray().Select(r => r.EnumerateArray().Select(x => x.GetDouble())),
            new Actual365Fixed(),
            context,
            VolatilityType.Normal,
            0.0);
        return StrippedOptionletAdapter.Create(stripped, context);
    }

    /// <summary>The test's normal swaption volatility matrix, flat-extrapolated: its <c>getSwaptionVTS</c>.</summary>
    /// <param name="spec">The base spec, its tenors and volatilities.</param>
    /// <param name="context">The evaluation context.</param>
    /// <returns>The structure.</returns>
    public static SwaptionVolatilityStructure SwaptionOf(JsonElement spec, EvaluationContext context) =>
        SwaptionVolatilityMatrix.Create(
            new TARGET(),
            BusinessDayConvention.Following,
            PeriodsOf(spec.GetProperty("optionTenors")),
            PeriodsOf(spec.GetProperty("swapTenors")),
            InflationSurfaceInputs.MatrixOf(spec.GetProperty("volatilities")),
            new Actual365Fixed(),
            context,
            true,
            VolatilityType.Normal);

    /// <summary>Periods, each [length, units].</summary>
    /// <param name="spec">The periods.</param>
    /// <returns>The periods.</returns>
    public static Period[] PeriodsOf(JsonElement spec) => [.. spec.EnumerateArray().Select(InflationInputs.LagOf)];
}
