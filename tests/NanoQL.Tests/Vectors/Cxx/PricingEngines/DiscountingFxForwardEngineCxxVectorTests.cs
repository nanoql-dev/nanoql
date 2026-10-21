// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Currencies;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// <see cref="DiscountingFxForwardEngine"/>'s harness vectors,
/// <c>tests/vectors/cxx/pricingengines/forward/discountingfxforwardengine.json</c>: the additional results of USD/SGD
/// forwards on flat curves dated on and after the evaluation and settlement dates, and the engine's refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class DiscountingFxForwardEngineCxxVectorTests
{
    private static readonly string[] Keys =
    [
        "spotFx", "sourceCurrencyDiscountFactor", "targetCurrencyDiscountFactor",
        "sourceCurrencySettlementDiscountFactor", "targetCurrencySettlementDiscountFactor", "sourceCurrencyPV",
        "targetCurrencyPV",
    ];

    [Fact]
    public void DiscountingFxForwardEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/forward/discountingfxforwardengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement f = c.Input("forward");
            var forward = new FxForward(
                f.GetProperty("sourceNominal").GetDouble(),
                new USDCurrency(),
                f.GetProperty("targetNominal").GetDouble(),
                new SGDCurrency(),
                new Date(f.GetProperty("maturityDate").GetInt32()),
                f.GetProperty("paySourceCurrency").GetBoolean(),
                f.GetProperty("settlementDays").GetInt32(),
                f.GetProperty("calendar").ValueKind == JsonValueKind.Null ? null : new TARGET());
            FxForwardResults Price() => DiscountingFxForwardEngine.Calculate(
                forward,
                c.Context(),
                DiscountingFxForwardEngineVectorTests.CurveOf(c.Input("sourceCurve")),
                DiscountingFxForwardEngineVectorTests.CurveOf(c.Input("targetCurve")),
                c.Double("spotFx"));
            foreach (string key in Keys)
            {
                check.Output(c, key, () => Price().Result<double>(key));
            }
        }

        check.Complete();
    }
}
