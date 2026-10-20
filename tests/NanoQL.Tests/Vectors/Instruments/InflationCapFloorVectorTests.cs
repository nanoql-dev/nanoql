// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using NanoQL.TermStructures;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The year-on-year inflation cap and floor vectors, <c>tests/vectors/instruments/inflationcapfloor.json</c>:
/// <see cref="YoYInflationCap"/>, <see cref="YoYInflationFloor"/> and <see cref="YoYInflationCollar"/> priced by each
/// engine, an expired cap, and the constructors' refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class InflationCapFloorVectorTests
{
    [Fact]
    public void InflationCapFloorVector()
    {
        var file = VectorFile.Load("instruments/inflationcapfloor");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            (InflationIndex index, EvaluationContext context) = InflationCashFlowInputs.IndexOf(c);
            var yii = (YoYInflationIndex)index;
            if (c.String("kind") == "construction")
            {
                check.Output(c, "construction", () => CapFloorOf(c.Input("instrument"), yii, context) is null ? 1 : 0);
                continue;
            }

            YoYInflationCapFloor capFloor = CapFloorOf(c.Input("instrument"), yii, context);
            InstrumentResults results = EngineOf(c.Input("engine"), capFloor, yii, c.Input("nominalCurve"), context);
            check.Output(c, "NPV", results.NPV);
            check.Output(c, "isExpired", capFloor.IsExpired(context));
            if (c.String("kind") == "capfloor")
            {
                check.Output(c, "optionletPrices", results.Result<ImmutableArray<double>>("optionletsPrice"));
            }
        }

        check.Complete();
    }

    /// <summary>The instrument an <c>"instrument"</c> spec describes.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="index">The index.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The instrument.</returns>
    internal static YoYInflationCapFloor CapFloorOf(JsonElement spec, YoYInflationIndex index, EvaluationContext context)
    {
        ImmutableArray<CashFlow> leg = YoYInflationCouponVectorTests.LegOf(spec.GetProperty("leg"), index, context);
        double[] caps = [.. spec.GetProperty("capRates").EnumerateArray().Select(x => x.GetDouble())];
        double[] floors = [.. spec.GetProperty("floorRates").EnumerateArray().Select(x => x.GetDouble())];
        return (YoYInflationCapFloorType)spec.GetProperty("type").GetInt32() switch
        {
            YoYInflationCapFloorType.Cap => new YoYInflationCap(leg, caps),
            YoYInflationCapFloorType.Floor => new YoYInflationFloor(leg, floors),
            _ => new YoYInflationCollar(leg, caps, floors),
        };
    }

    /// <summary>An instrument priced by the engine an <c>"engine"</c> spec describes.</summary>
    /// <param name="spec">The spec: the engine's kind and its volatility.</param>
    /// <param name="capFloor">The instrument.</param>
    /// <param name="index">The index the forwards are observed on.</param>
    /// <param name="nominalCurve">The nominal curve's spec.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The results.</returns>
    internal static InstrumentResults EngineOf(
        JsonElement spec, YoYInflationCapFloor capFloor, YoYInflationIndex index, JsonElement nominalCurve, EvaluationContext context)
    {
        YoYOptionletVolatilitySurface vol = InflationInputs.YoYVolatilityOf(spec.GetProperty("volatility"), context);
        YieldTermStructure nominal = CashFlowInputs.CurveOf(nominalCurve);
        return spec.GetProperty("kind").GetString()! switch
        {
            "Black" => YoYInflationBlackCapFloorEngine.Calculate(capFloor, context, index, vol, nominal),
            "UnitDisplacedBlack" => YoYInflationUnitDisplacedBlackCapFloorEngine.Calculate(capFloor, context, index, vol, nominal),
            "Bachelier" => YoYInflationBachelierCapFloorEngine.Calculate(capFloor, context, index, vol, nominal),
            string kind => throw new InvalidOperationException($"unknown engine '{kind}'"),
        };
    }
}
