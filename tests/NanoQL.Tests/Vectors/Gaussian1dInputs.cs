// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// Builds the Gaussian1d models and engine settings that the Gaussian1d vector files describe in their inputs, as their
/// generators build them (<c>tools/vectors/gen/pricingengines/swaption/gaussian1dswaptionengine.py</c>'s functions).
/// </summary>
/// <remarks>
/// A GSR model is <c>{"curve", "volstepdates", "volatilities", "reversions", "T"}</c>, its curve a flat forward spec of
/// <see cref="CashFlowInputs"/>, its step dates serial numbers; engine settings are <c>{"integrationPoints",
/// "stddevs", "extrapolatePayoff", "discountCurve"}</c>, the discount curve a flat forward spec or <c>null</c>.
/// </remarks>
internal static class Gaussian1dInputs
{
    /// <summary>The GSR model a spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The model.</returns>
    public static Gsr GsrOf(JsonElement spec) =>
        new(
            CashFlowInputs.CurveOf(spec.GetProperty("curve")),
            spec.GetProperty("volstepdates").EnumerateArray().Select(d => new Date(d.GetInt32())).ToArray(),
            Numbers(spec.GetProperty("volatilities")),
            Numbers(spec.GetProperty("reversions")),
            spec.GetProperty("T").GetDouble());

    /// <summary>
    /// A vanilla swap of QuantLib's <c>MakeVanillaSwap(tenor, index, strike, 0D, effectiveDate=start)</c>, the start
    /// two TARGET business days after the exercise unless given, as the divergence and convention tests build them.
    /// </summary>
    /// <param name="index">The index.</param>
    /// <param name="exercise">The exercise date.</param>
    /// <param name="years">The swap's length in years.</param>
    /// <param name="strike">The fixed rate.</param>
    /// <param name="type">Payer or receiver.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="fixedTenor">The fixed leg's tenor; a year by default.</param>
    /// <param name="start">The swap's start, or <see langword="null"/> for two business days after the
    /// exercise.</param>
    /// <returns>The swap.</returns>
    public static Instruments.VanillaSwap SwapOf(
        Indexes.IborIndex index, Date exercise, int years, double strike, Instruments.SwapType type, EvaluationContext context, Period? fixedTenor = null, Date? start = null) =>
        new Instruments.MakeVanillaSwap(new Period(years, TimeUnit.Years), index)
            .WithFixedRate(strike)
            .WithForwardStart(new Period(0, TimeUnit.Days))
            .WithEffectiveDate(start ?? new TARGET().Advance(exercise, 2, TimeUnit.Days))
            .WithFixedLegTenor(fixedTenor ?? new Period(1, TimeUnit.Years))
            .WithType(type)
            .ToVanillaSwap(context);

    /// <summary>A constant-parameter GSR model with no step dates.</summary>
    /// <param name="curve">The model's curve.</param>
    /// <param name="reversion">The reversion.</param>
    /// <param name="volatility">The volatility.</param>
    /// <returns>The model.</returns>
    public static Gsr Constant(YieldTermStructure curve, double reversion, double volatility) => new(curve, [], [volatility], reversion);

    /// <summary>A swaption's price on a Gaussian1d model with the engine settings a spec describes.</summary>
    /// <param name="swaption">The swaption.</param>
    /// <param name="context">The evaluation context.</param>
    /// <param name="model">The model.</param>
    /// <param name="settings">The settings spec, or <see langword="null"/> for the engine's defaults.</param>
    /// <returns>The NPV.</returns>
    public static double? Price(Instruments.Swaption swaption, EvaluationContext context, Gaussian1dModel model, JsonElement? settings = null)
    {
        if (settings is not JsonElement s)
        {
            return Gaussian1dSwaptionEngine.Calculate(swaption, context, model).NPV;
        }

        JsonElement curve = s.GetProperty("discountCurve");
        YieldTermStructure? discountCurve = curve.ValueKind == JsonValueKind.Null ? null : CashFlowInputs.CurveOf(curve);
        return Gaussian1dSwaptionEngine.Calculate(
            swaption,
            context,
            model,
            s.GetProperty("integrationPoints").GetInt32(),
            s.GetProperty("stddevs").GetDouble(),
            s.GetProperty("extrapolatePayoff").GetBoolean(),
            false,
            discountCurve).NPV;
    }
}
