// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Gaussian1d smile section vectors,
/// <c>tests/vectors/cxx/termstructures/volatility/gaussian1dsmilesection.json</c>: <see cref="Gaussian1dSmileSection"/>
/// on a Euribor swap index, three GSR models, curves attached to the index or the model's, the default engine and one
/// given; its level, exercise date and time, reference date and strike range, and at strikes around the level its call
/// and put prices at two discounts and its volatility where stable (the S7 plan's S7R6).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class Gaussian1dSmileSectionCxxVectorTests
{
    [Fact]
    public void Gaussian1dSmileSectionCxxVector()
    {
        var file = VectorFile.Load("cxx/termstructures/volatility/gaussian1dsmilesection");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            Gaussian1dSmileSection s = SectionOf(c.Input("section"), context);
            double[] strikes = VolatilityInputs.Reals(c.Input("strikes"));
            double[] discounts = VolatilityInputs.Reals(c.Input("discounts"));
            check.Output(c, "atmLevel", s.AtmLevel());
            check.Output(c, "exerciseDate", s.ExerciseDate);
            check.Output(c, "exerciseTime", s.ExerciseTime);
            check.Output(c, "referenceDate", () => s.ReferenceDate());
            check.Output(c, "minStrike", s.MinStrike());
            check.Output(c, "maxStrike", s.MaxStrike());
            foreach ((string name, OptionType type) in new[] { ("callPrice", OptionType.Call), ("putPrice", OptionType.Put) })
            {
                check.Output(
                    c,
                    name,
                    discounts.Select(d => strikes.Select(k => (Func<object?>)(() => s.OptionPrice(k, type, d))).ToArray()).ToArray());
            }

            check.Output(c, "volatility", strikes.Select(k => (Func<object?>)(() => s.Volatility(k))).ToArray());
        }

        check.Complete();
    }

    // The section a spec describes: a GSR model, a Euribor swap index with its curves or none, and the default engine
    // or a Gaussian1d swaption engine.
    private static Gaussian1dSmileSection SectionOf(JsonElement spec, EvaluationContext context)
    {
        Gsr model = Gaussian1dInputs.GsrOf(spec.GetProperty("model"));
        return new Gaussian1dSmileSection(
            new Date(spec.GetProperty("fixingDate").GetInt32()),
            SwapIndexOf(spec.GetProperty("swapIndex")),
            model,
            CashFlowInputs.DayCounterOf(spec.GetProperty("dayCounter")),
            context,
            EngineOf(spec.GetProperty("engine"), model));
    }

    /// <summary>The harness's swap index spec: a Euribor swap index with its forwarding and discounting curves, each
    /// null for none.</summary>
    /// <param name="spec">The spec.</param>
    /// <returns>The swap index.</returns>
    internal static SwapIndex SwapIndexOf(JsonElement spec) => new EuriborSwapIsdaFixA(
        SwapInputs.PeriodOf(spec.GetProperty("tenor")), CurveOrNull(spec.GetProperty("forwarding")), CurveOrNull(spec.GetProperty("discounting")));

    /// <summary>The harness's engine spec: null for the section's default, or a Gaussian1d swaption engine on the
    /// model.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="model">The model.</param>
    /// <returns>The engine, or <see langword="null"/>.</returns>
    internal static Func<Swaption, EvaluationContext, InstrumentResults>? EngineOf(JsonElement spec, Gsr model)
    {
        if (spec.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        int points = spec.GetProperty("integrationPoints").GetInt32();
        double stddevs = spec.GetProperty("stddevs").GetDouble();
        bool extrapolate = spec.GetProperty("extrapolatePayoff").GetBoolean();
        YieldTermStructure? discount = CurveOrNull(spec.GetProperty("discountCurve"));
        return (swaption, ctx) => Gaussian1dSwaptionEngine.Calculate(swaption, ctx, model, points, stddevs, extrapolate, false, discount);
    }

    private static YieldTermStructure? CurveOrNull(JsonElement spec) =>
        spec.ValueKind == JsonValueKind.Null ? null : CashFlowInputs.CurveOf(spec);
}
