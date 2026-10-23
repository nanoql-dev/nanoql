// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The wheel's Gaussian1d float-float swaption engine vectors,
/// <c>tests/vectors/pricingengines/swaption/gaussian1dfloatfloatswaptionengine.json</c>: European and Bermudan
/// float-float swaptions (Euribor 3M against 6M, a gearing with spreads at the money, amortizing with the capital
/// exchanges, caps and floors, CMS legs, expired and seasoned exercises) on three GSR models, the engine's settings
/// varied on the first, an option-adjusted spread, the underlying's value, today's exercise included, and the naive and
/// digital probabilities.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class Gaussian1dFloatFloatSwaptionEngineVectorTests
{
    [Fact]
    public void Gaussian1dFloatFloatSwaptionEngineVector()
    {
        var file = VectorFile.Load("pricingengines/swaption/gaussian1dfloatfloatswaptionengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            FloatFloatSwaption swaption = FloatFloatInputs.SwaptionOf(c.Input("swaption"), context);
            Gsr[] models = [.. c.Input("models").EnumerateArray().Select(Gaussian1dInputs.GsrOf)];
            check.Output(c, "gsr", models.Select(m => (Func<object?>)(() => Price(swaption, context, m))));
            check.Output(
                c,
                "settings",
                c.Input("engineSettings").EnumerateArray().Select(s => (Func<object?>)(() => Price(swaption, context, models[0], s))));
            if (c.Expected.TryGetProperty("underlyingValue", out _))
            {
                check.Output(
                    c,
                    "underlyingValue",
                    () => Gaussian1dFloatFloatSwaptionEngine.Calculate(swaption, context, models[0]).Result<double>("underlyingValue"));
            }

            double oas = c.Input("oas").GetDouble();
            check.Output(c, "oas", () => Gaussian1dFloatFloatSwaptionEngine.Calculate(swaption, context, models[0], oas: oas).NPV);
            check.Output(
                c,
                "includeToday",
                () => Gaussian1dFloatFloatSwaptionEngine.Calculate(swaption, context, models[0], includeTodaysExercise: true).NPV);
            foreach ((string label, Gaussian1dFloatFloatSwaptionEngineProbabilities kind) in new[]
            {
                ("naive", Gaussian1dFloatFloatSwaptionEngineProbabilities.Naive),
                ("digital", Gaussian1dFloatFloatSwaptionEngineProbabilities.Digital),
            })
            {
                if (!c.Expected.TryGetProperty(label + "NPV", out _))
                {
                    continue;
                }

                InstrumentResults r = Gaussian1dFloatFloatSwaptionEngine.Calculate(swaption, context, models[0], probabilities: kind);
                check.Output(c, label + "NPV", r.NPV);
                check.Output(c, label + "Probabilities", r.Result<ImmutableArray<double>>("probabilities"));
            }
        }

        check.Complete();
    }

    private static double? Price(FloatFloatSwaption swaption, EvaluationContext context, Gsr model, JsonElement? settings = null)
    {
        if (settings is not JsonElement s)
        {
            return Gaussian1dFloatFloatSwaptionEngine.Calculate(swaption, context, model).NPV;
        }

        JsonElement curve = s.GetProperty("discountCurve");
        YieldTermStructure? discountCurve = curve.ValueKind == JsonValueKind.Null ? null : CashFlowInputs.CurveOf(curve);
        return Gaussian1dFloatFloatSwaptionEngine.Calculate(
            swaption,
            context,
            model,
            s.GetProperty("integrationPoints").GetInt32(),
            s.GetProperty("stddevs").GetDouble(),
            s.GetProperty("extrapolatePayoff").GetBoolean(),
            false,
            null,
            discountCurve).NPV;
    }
}
