// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The swaption engine vectors, <c>tests/vectors/pricingengines/swaption/blackswaptionengine.json</c>: swaptions priced by
/// the Black and Bachelier engines on a volatility and on a volatility structure (constant or a matrix), each with both
/// cash annuity models; the NPV and the results the wheel reads; and the engines' refusal of a structure of the other
/// volatility type.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BlackSwaptionEngineVectorTests
{
    [Fact]
    public void BlackSwaptionEngineVector()
    {
        var file = VectorFile.Load("pricingengines/swaption/blackswaptionengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            if (c.Inputs.TryGetProperty("engine", out JsonElement engine))
            {
                // The engine's refusal of the structure: QuantLib's constructor check, nanoql's first.
                SwaptionVolatilityStructure structure = StructureOf(c.Input("structure"), context);
                check.Output(c, "engine", () => Probe(engine.GetString()!, structure));
                continue;
            }

            Swaption swaption = SwaptionInputs.SwaptionOf(c.Input("swaption"), context);
            YieldTermStructure discountCurve = CashFlowInputs.CurveOf(c.Input("discountCurve"));
            JsonElement black = c.Input("blackQuote"), bachelier = c.Input("bachelierQuote");
            SwaptionVolatilityStructure blackStructure = StructureOf(c.Input("blackStructure"), context);
            SwaptionVolatilityStructure bachelierStructure = StructureOf(c.Input("bachelierStructure"), context);
            foreach ((string name, CashAnnuityModel model) in (ReadOnlySpan<(string, CashAnnuityModel)>)
                [("SwapRate", CashAnnuityModel.SwapRate), ("DiscountCurve", CashAnnuityModel.DiscountCurve)])
            {
                SwaptionInputs.CheckPriced(check, c, "blackQuote" + name, () => BlackSwaptionEngine.Calculate(
                    swaption,
                    context,
                    discountCurve,
                    IndexInputs.Real(black.GetProperty("vol")),
                    CashFlowInputs.DayCounterOf(black.GetProperty("dayCounter")),
                    IndexInputs.Real(black.GetProperty("displacement")),
                    model));
                SwaptionInputs.CheckPriced(
                    check,
                    c,
                    "blackStructure" + name,
                    () => BlackSwaptionEngine.Calculate(swaption, context, discountCurve, blackStructure, model));
                SwaptionInputs.CheckPriced(check, c, "bachelierQuote" + name, () => BachelierSwaptionEngine.Calculate(
                    swaption,
                    context,
                    discountCurve,
                    IndexInputs.Real(bachelier.GetProperty("vol")),
                    CashFlowInputs.DayCounterOf(bachelier.GetProperty("dayCounter")),
                    model));
                SwaptionInputs.CheckPriced(
                    check,
                    c,
                    "bachelierStructure" + name,
                    () => BachelierSwaptionEngine.Calculate(swaption, context, discountCurve, bachelierStructure, model));
            }
        }

        check.Complete();
    }

    // A constant volatility of swaptionconstantvol.py's specs, or a matrix of swaptionvolmatrix.py's.
    private static SwaptionVolatilityStructure StructureOf(JsonElement spec, EvaluationContext context) =>
        spec.TryGetProperty("form", out _)
            ? SwaptionInputs.MatrixOf(spec, context)
            : VolatilityInputs.SwaptionVolatilityOf(spec, context);

    // Prices a swaption with the engine on the structure, so that the engine's own refusal of it comes first.
    private static object? Probe(string engine, SwaptionVolatilityStructure structure)
    {
        var context = new EvaluationContext { EvaluationDate = structure.ReferenceDate };
        var curve = new FlatForward(structure.ReferenceDate, 0.03, new Actual365Fixed());
        var swap = new MakeVanillaSwap(new Period(1, TimeUnit.Years), new NanoQL.Indexes.Euribor6M(curve))
            .WithFixedRate(0.03)
            .WithForwardStart(new Period(1, TimeUnit.Years))
            .ToVanillaSwap(context);
        var swaption = new Swaption(swap, new EuropeanExercise(structure.ReferenceDate + 30));
        return engine == "black"
            ? BlackSwaptionEngine.Calculate(swaption, context, curve, structure)
            : BachelierSwaptionEngine.Calculate(swaption, context, curve, structure);
    }
}
