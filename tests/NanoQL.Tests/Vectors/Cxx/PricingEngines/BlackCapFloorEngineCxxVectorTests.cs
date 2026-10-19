// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's cap and floor engine vectors, <c>tests/vectors/cxx/pricingengines/capfloor/blackcapfloorengine.json</c>:
/// <see cref="BlackCapFloorEngine"/> and <see cref="BachelierCapFloorEngine"/> on a volatility with a day counter, which
/// the wheel's Bachelier engine lacks, and on optionlet volatility structures, with the structure forms' checks of the
/// volatility type and the displacement.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BlackCapFloorEngineCxxVectorTests
{
    [Fact]
    public void BlackCapFloorEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/capfloor/blackcapfloorengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            CapFloor capFloor = CapFloorInputs.CapFloorOf(c.Input("capFloor"), context);
            YieldTermStructure curve = CashFlowInputs.CurveOf(c.Input("discountCurve"));
            string[] tags = [.. c.Input("tags").EnumerateArray().Select(t => t.GetString()!)];
            object?[] values =
            [
                .. c.Input("engines").EnumerateArray().Select(e => (object?)tags
                    .Select(tag => (object?)(Func<object?>)(() => Result(Price(capFloor, context, curve, e), tag)))
                    .ToArray()),
            ];
            check.Output(c, "values", values);
        }

        check.Complete();
    }

    private static InstrumentResults Price(CapFloor capFloor, EvaluationContext context, YieldTermStructure curve, JsonElement engine)
    {
        bool black = engine.GetProperty("type").GetString() == "BlackCapFloorEngine";
        if (engine.GetProperty("form").GetString() == "volatility")
        {
            double vol = engine.GetProperty("vol").GetDouble();
            var dc = CashFlowInputs.DayCounterOf(engine.GetProperty("dayCounter"));
            return black
                ? BlackCapFloorEngine.Calculate(capFloor, context, curve, vol, dc, engine.GetProperty("displacement").GetDouble())
                : BachelierCapFloorEngine.Calculate(capFloor, context, curve, vol, dc);
        }

        OptionletVolatilityStructure structure = VolatilityInputs.OptionletVolatilityOf(engine.GetProperty("volatility"), context);
        return black
            ? BlackCapFloorEngine.Calculate(capFloor, context, curve, structure, CouponInputs.NullableReal(engine.GetProperty("displacement")))
            : BachelierCapFloorEngine.Calculate(capFloor, context, curve, structure);
    }

    private static object? Result(InstrumentResults results, string tag) => tag switch
    {
        "NPV" => results.NPV,
        "vega" => results.Result<double>(tag),
        "optionletsAtmForward" => results.Result<ImmutableArray<double?>>(tag),
        _ => results.Result<ImmutableArray<double>>(tag),
    };
}
