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
/// The wheel's Gaussian1d nonstandard swaption engine vectors,
/// <c>tests/vectors/pricingengines/swaption/gaussian1dnonstandardswaptionengine.json</c>: European and Bermudan
/// nonstandard swaptions (constant terms, amortizing with step-up rates, gearings and spreads, the capital exchanges, a
/// rebated exercise, an overnight index paid late, expired and seasoned exercises) on three GSR models, the engine's
/// settings varied on the first, an option-adjusted spread, and the naive and digital probabilities.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class Gaussian1dNonstandardSwaptionEngineVectorTests
{
    [Fact]
    public void Gaussian1dNonstandardSwaptionEngineVector()
    {
        var file = VectorFile.Load("pricingengines/swaption/gaussian1dnonstandardswaptionengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            NonstandardSwaption swaption = NonstandardInputs.SwaptionOf(c.Input("swaption"), context);
            Gsr[] models = [.. c.Input("models").EnumerateArray().Select(Gaussian1dInputs.GsrOf)];
            check.Output(c, "gsr", models.Select(m => (Func<object?>)(() => Price(swaption, context, m))));
            check.Output(
                c,
                "settings",
                c.Input("engineSettings").EnumerateArray().Select(s => (Func<object?>)(() => Price(swaption, context, models[0], s))));
            double oas = c.Input("oas").GetDouble();
            check.Output(c, "oas", () => Gaussian1dNonstandardSwaptionEngine.Calculate(swaption, context, models[0], oas: oas).NPV);
            foreach ((string label, Gaussian1dNonstandardSwaptionEngineProbabilities kind) in new[]
            {
                ("naive", Gaussian1dNonstandardSwaptionEngineProbabilities.Naive),
                ("digital", Gaussian1dNonstandardSwaptionEngineProbabilities.Digital),
            })
            {
                if (!c.Expected.TryGetProperty(label + "NPV", out _))
                {
                    continue;
                }

                InstrumentResults r = Gaussian1dNonstandardSwaptionEngine.Calculate(swaption, context, models[0], probabilities: kind);
                check.Output(c, label + "NPV", r.NPV);
                check.Output(c, label + "Probabilities", r.Result<ImmutableArray<double>>("probabilities"));
            }
        }

        check.Complete();
    }

    private static double? Price(NonstandardSwaption swaption, EvaluationContext context, Gsr model, JsonElement? settings = null)
    {
        if (settings is not JsonElement s)
        {
            return Gaussian1dNonstandardSwaptionEngine.Calculate(swaption, context, model).NPV;
        }

        JsonElement curve = s.GetProperty("discountCurve");
        YieldTermStructure? discountCurve = curve.ValueKind == JsonValueKind.Null ? null : CashFlowInputs.CurveOf(curve);
        return Gaussian1dNonstandardSwaptionEngine.Calculate(
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
