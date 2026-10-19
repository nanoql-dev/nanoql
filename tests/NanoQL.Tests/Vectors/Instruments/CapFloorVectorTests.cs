// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The cap and floor vectors, <c>tests/vectors/instruments/capfloor.json</c>: <see cref="Cap"/>s, <see cref="Floor"/>s
/// and <see cref="Collar"/>s on Ibor legs, their inspectors, expiry, at-the-money rate and NPVs on
/// <see cref="PricingEngines.BlackCapFloorEngine"/> and <see cref="PricingEngines.BachelierCapFloorEngine"/>, and
/// <see cref="CapFloor.ImpliedVolatility"/>'s solves, whose implied volatility a case holds only where the wheel's own
/// solve is stable under one-ulp moves of its price.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CapFloorVectorTests
{
    [Fact]
    public void CapFloorVector()
    {
        var file = VectorFile.Load("instruments/capfloor");
        var check = new VectorAssert(file);
        var instruments = new Dictionary<string, VectorCase>();
        foreach (VectorCase c in file.Cases)
        {
            if (c.Inputs.TryGetProperty("capFloor", out _))
            {
                instruments[c.Name] = c;
                CheckInstrument(check, c);
            }
            else
            {
                CheckSolve(check, c, instruments[c.String("instrument")]);
            }
        }

        check.Complete();
    }

    private static void CheckInstrument(VectorAssert check, VectorCase c)
    {
        EvaluationContext context = c.Context();
        JsonElement spec = c.Input("capFloor");
        CapFloor? capFloor = null;
        check.Output(c, "built", () =>
        {
            capFloor = CapFloorInputs.CapFloorOf(spec, context);
            return true;
        });
        if (capFloor is null)
        {
            return;
        }

        YieldTermStructure curve = CashFlowInputs.CurveOf(c.Input("discountCurve"));
        JsonElement black = JsonDocument.Parse("""{"vol": 0.2, "dayCounter": {"type": "Actual365Fixed"}, "displacement": 0.0}""").RootElement;
        JsonElement bachelier = JsonDocument.Parse("""{"vol": 0.008}""").RootElement;
        check.Output(c, "type", capFloor.Type);
        check.Output(c, "capRates", capFloor.CapRates);
        check.Output(c, "floorRates", capFloor.FloorRates);
        check.Output(c, "startDate", () => capFloor.StartDate());
        check.Output(c, "maturityDate", () => capFloor.MaturityDate());
        check.Output(c, "isExpired", capFloor.IsExpired(context));
        check.Output(c, "atmRate", () => capFloor.AtmRate(curve, context));
        check.Output(c, "blackNPV", () => CapFloorInputs.Black(capFloor, context, curve, black).NPV);
        check.Output(c, "bachelierNPV", () => CapFloorInputs.Bachelier(capFloor, context, curve, bachelier).NPV);
    }

    private static void CheckSolve(VectorAssert check, VectorCase c, VectorCase instrument)
    {
        EvaluationContext context = c.Context();
        CapFloor capFloor = CapFloorInputs.CapFloorOf(instrument.Input("capFloor"), context);
        YieldTermStructure curve = CashFlowInputs.CurveOf(instrument.Input("discountCurve"));
        double price = IndexInputs.Real(c.Input("price"));

        // The target: the wheel engine's NPV at the case's volatility, which nanoql's engine reprices, or a chosen price.
        JsonElement from = c.Input("priceFrom");
        check.Output(c, "price", () => from.GetProperty("engine").GetString() switch
        {
            "black" => BlackCapFloorEngine.Calculate(
                capFloor, context, curve, from.GetProperty("vol").GetDouble(), null, from.GetProperty("displacement").GetDouble()).NPV,
            "bachelier" => BachelierCapFloorEngine.Calculate(capFloor, context, curve, from.GetProperty("vol").GetDouble()).NPV,
            _ => price,
        });
        if (c.Expected.TryGetProperty("impliedVolatility", out _))
        {
            check.Output(c, "impliedVolatility", () => capFloor.ImpliedVolatility(
                price,
                curve,
                c.Double("guess"),
                context,
                c.Double("accuracy"),
                c.Int("maxEvaluations"),
                c.Double("minVol"),
                c.Double("maxVol"),
                (VolatilityType)c.Int("type"),
                c.Double("displacement")));
        }
    }
}
