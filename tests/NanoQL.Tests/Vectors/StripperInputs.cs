// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The optionlet stripping vectors' inputs (<c>tools/vectors/gen/termstructures/volatility/optionlet/</c>): curves,
/// indexes, strippers and stripped optionlets.
/// </summary>
internal static class StripperInputs
{
    /// <summary>The curve a spec describes: a flat forward of the index generator, or QuantLib's <c>ZeroCurve</c>.</summary>
    /// <param name="spec">The spec, or JSON <c>null</c> for none.</param>
    /// <returns>The curve, or <see langword="null"/>.</returns>
    public static YieldTermStructure? CurveOf(JsonElement spec)
    {
        if (spec.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        if (spec.TryGetProperty("type", out JsonElement type) && type.GetString() == "ZeroCurve")
        {
            Date[] dates = [.. spec.GetProperty("dates").EnumerateArray().Select(d => new Date(d.GetInt32()))];
            return new InterpolatedZeroCurve<Linear>(
                dates, InterpolationInputs.Numbers(spec.GetProperty("rates")),
                CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter")),
                CashFlowInputs.CalendarOf(spec.GetProperty("calendar")));
        }

        return CashFlowInputs.CurveOf(spec);
    }

    /// <summary>The index of a spec's type on a curve.</summary>
    /// <param name="type">"Euribor6M", "Euribor3M" or "Sofr".</param>
    /// <param name="curve">The forwarding curve, or <see langword="null"/>.</param>
    /// <returns>The index.</returns>
    public static IborIndex IndexOf(string type, YieldTermStructure? curve) => type switch
    {
        "Euribor6M" => new Euribor6M(curve),
        "Euribor3M" => new Euribor3M(curve),
        "Sofr" => new Sofr(curve),
        _ => throw new ArgumentException($"unknown index {type}", nameof(type)),
    };

    /// <summary>The stripper of an <c>optionletstripper1</c> case's inputs.</summary>
    /// <param name="inputs">The case's inputs (or a source spec holding the same keys).</param>
    /// <param name="context">The case's context.</param>
    /// <returns>The stripper.</returns>
    public static OptionletStripper1 StripperOf(JsonElement inputs, EvaluationContext context)
    {
        CapFloorTermVolSurface surface =
            CapFloorTermVolSurfaceVectorTests.SurfaceOf(inputs.GetProperty("surface"), context);
        IborIndex index = IndexOf(
            inputs.GetProperty("index").GetProperty("type").GetString()!, CurveOf(inputs.GetProperty("curve")));
        JsonElement switchStrike = inputs.GetProperty("switchStrike");
        JsonElement frequency = inputs.GetProperty("optionletFrequency");
        return new OptionletStripper1(
            surface,
            index,
            context,
            switchStrike.ValueKind == JsonValueKind.Null ? null : InterpolationInputs.Number(switchStrike),
            InterpolationInputs.Number(inputs.GetProperty("accuracy")),
            inputs.GetProperty("maxIter").GetInt32(),
            CurveOf(inputs.GetProperty("discount")),
            (VolatilityType)inputs.GetProperty("type").GetInt32(),
            InterpolationInputs.Number(inputs.GetProperty("displacement")),
            inputs.GetProperty("dontThrow").GetBoolean(),
            frequency.ValueKind == JsonValueKind.Null ? null : SwapInputs.PeriodOf(frequency),
            inputs.GetProperty("paymentLag").GetInt32());
    }

    /// <summary>The stripped optionlet a <c>strippedoptionlet</c> spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="context">The case's context.</param>
    /// <returns>The stripped optionlet.</returns>
    public static StrippedOptionlet StrippedOptionletOf(JsonElement spec, EvaluationContext context)
    {
        JsonElement index = spec.GetProperty("index");
        return new StrippedOptionlet(
            spec.GetProperty("settlementDays").GetInt32(),
            CashFlowInputs.CalendarOf(spec.GetProperty("calendar")),
            (BusinessDayConvention)spec.GetProperty("convention").GetInt32(),
            IndexOf(index.GetProperty("type").GetString()!, CurveOf(index.GetProperty("curve"))),
            [.. spec.GetProperty("optionletDates").EnumerateArray().Select(d => new Date(d.GetInt32()))],
            InterpolationInputs.Numbers(spec.GetProperty("strikes")),
            [.. spec.GetProperty("volatilities").EnumerateArray().Select(InterpolationInputs.Numbers)],
            CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter")),
            context,
            (VolatilityType)spec.GetProperty("type").GetInt32(),
            InterpolationInputs.Number(spec.GetProperty("displacement")));
    }

    /// <summary>A matrix's rows, as the vectors write them.</summary>
    /// <param name="m">The matrix.</param>
    /// <returns>Its rows.</returns>
    public static double[][] Rows(Matrix m) => [.. Enumerable.Range(0, m.Rows).Select(i => m.Row(i).ToArray())];
}
