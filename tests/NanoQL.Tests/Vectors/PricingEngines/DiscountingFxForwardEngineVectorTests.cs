// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The DiscountingFxForwardEngine vectors, <c>tests/vectors/pricingengines/forward/discountingfxforwardengine.json</c>:
/// forwards' NPVs, fair forward rates and NPVs in each currency on flat curves dated on, between and after the
/// evaluation and settlement dates, with spot rates above and below 1, and the engine's refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class DiscountingFxForwardEngineVectorTests
{
    [Fact]
    public void DiscountingFxForwardEngineVector()
    {
        var file = VectorFile.Load("pricingengines/forward/discountingfxforwardengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            FxForwardResults Price() => PriceOf(c);
            check.Output(c, "NPV", () => Price().NPV);
            check.Output(c, "fairForwardRate", () => Price().FairForwardRate);
            check.Output(c, "npvSourceCurrency", () => Price().NpvSourceCurrency);
            check.Output(c, "npvTargetCurrency", () => Price().NpvTargetCurrency);
        }

        check.Complete();
    }

    /// <summary>A case's forward priced on its curves and spot, under its context.</summary>
    /// <param name="c">The case.</param>
    /// <returns>The results.</returns>
    internal static FxForwardResults PriceOf(VectorCase c) =>
        DiscountingFxForwardEngine.Calculate(
            FxForwardVectorTests.ForwardOf(c.Input("forward")),
            c.Context(),
            CurveOf(c.Input("sourceCurve")),
            CurveOf(c.Input("targetCurve")),
            c.Double("spotFx"));

    /// <summary>A flat curve as the vectors write it: <c>{referenceDate, rate, dayCounter}</c>.</summary>
    /// <param name="e">The JSON value.</param>
    /// <returns>The curve.</returns>
    internal static YieldTermStructure CurveOf(JsonElement e) =>
        new FlatForward(
            new Date(e.GetProperty("referenceDate").GetInt32()),
            e.GetProperty("rate").GetDouble(),
            e.GetProperty("dayCounter").GetString() switch
            {
                "Actual365Fixed" => new Actual365Fixed(),
                "Actual360" => new Actual360(),
                var other => throw new ArgumentException($"unknown day counter {other}", nameof(e)),
            });
}
