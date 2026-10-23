// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Models;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The wheel's float-float swaption vectors, <c>tests/vectors/instruments/floatfloatswaption.json</c>: on six Bermudan
/// float-float swaptions, the calibration basket of each type their Gaussian1d engine generates on a Euribor standard
/// swap base against a constant swaption volatility, per helper its swaption's expiry and maturity dates, nominal and
/// strike, its volatility and market value, each where stable (the S7 plan's S7R6).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FloatFloatSwaptionVectorTests
{
    [Fact]
    public void FloatFloatSwaptionVector()
    {
        var file = VectorFile.Load("instruments/floatfloatswaption");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            FloatFloatSwaption swaption = FloatFloatInputs.SwaptionOf(c.Input("swaption"), context);
            System.Text.Json.JsonElement market = c.Input("market");
            YieldTermStructure forecast = CashFlowInputs.CurveOf(market.GetProperty("forecast"));
            YieldTermStructure discount = CashFlowInputs.CurveOf(market.GetProperty("discount"));
            var engine = new Gaussian1dFloatFloatSwaptionEngine(Gaussian1dInputs.GsrOf(c.Input("model")), discountCurve: discount);
            var index = new EuriborSwapIsdaFixA(SwapInputs.PeriodOf(market.GetProperty("swapIndexTenor")), forecast, discount);
            SwaptionVolatilityStructure vol = ConstantSwaptionVolatility.Create(
                0, new TARGET(), BusinessDayConvention.ModifiedFollowing, market.GetProperty("volatility").GetDouble(), new Actual365Fixed(), context);
            foreach ((string name, BasketGeneratingEngineCalibrationBasketType type) in new[]
            {
                ("Naive", BasketGeneratingEngineCalibrationBasketType.Naive),
                ("MaturityStrikeByDeltaGamma", BasketGeneratingEngineCalibrationBasketType.MaturityStrikeByDeltaGamma),
            })
            {
                ImmutableArray<SwaptionHelper> basket = swaption.CalibrationBasket(engine, index, vol, context, type);
                Check(check, c, name + ".expiry", () => basket.Select(h => h.Swaption.Exercise.LastDate()).ToArray());
                Check(check, c, name + ".maturity", () => basket.Select(h => h.Underlying.MaturityDate()).ToArray());
                Check(check, c, name + ".nominal", () => basket.Select(h => h.Underlying.Nominal()).ToArray());
                Check(check, c, name + ".strike", () => basket.Select(h => h.Underlying.FixedRate).ToArray());
                Check(check, c, name + ".volatility", () => basket.Select(h => h.Volatility).ToArray());
                Check(check, c, name + ".marketValue", () => basket.Select(h => h.MarketValue).ToArray());
            }
        }

        check.Complete();
    }

    private static void Check(VectorAssert check, VectorCase c, string name, Func<object?> value)
    {
        if (c.Expected.TryGetProperty(name, out _))
        {
            check.Output(c, name, value);
        }
    }
}
