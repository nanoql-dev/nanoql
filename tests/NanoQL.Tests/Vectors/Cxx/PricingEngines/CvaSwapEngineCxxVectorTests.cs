// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The counterparty-risk adjusted swap engine's harness vectors,
/// <c>tests/vectors/cxx/pricingengines/swap/cvaswapengine.json</c>:
/// <see cref="CounterpartyAdjSwapEngine"/>'s NPV and fair rate on vanilla swaps at their fair rates, fresh and
/// seasoned, its exposure priced by Black swaptions on a volatility or a quote, or by Bachelier swaptions, with and
/// without an investor curve. At its fair rate a swap's swaptionlets are struck alike by QuantLib and nanoql; off par
/// they part (<c>Divergences/CvaSwaptionStrikeTests</c>).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CvaSwapEngineCxxVectorTests
{
    [Fact]
    public void CvaSwapEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/swap/cvaswapengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            FixedVsFloatingSwapResults results = Price(c, context);
            check.Output(c, "NPV", results.NPV);
            check.Output(c, "fairRate", results.FairRate);
        }

        check.Complete();
    }

    /// <summary>The engine's results on a case's swap, curves and exposure model.</summary>
    /// <param name="c">The case.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The results.</returns>
    internal static FixedVsFloatingSwapResults Price(VectorCase c, EvaluationContext context)
    {
        VanillaSwap swap = SwapInputs.VanillaSwapOf(c.Input("swap"), context);
        YieldTermStructure discount = IndexInputs.CurveOf(c.Input("discountCurve"))!;
        DefaultProbabilityTermStructure ctpty = CreditInputs.CurveOf(c.Input("ctptyCurve"), context);
        JsonElement investorSpec = c.Input("investorCurve");
        DefaultProbabilityTermStructure? investor =
            investorSpec.ValueKind == JsonValueKind.Null ? null : CreditInputs.CurveOf(investorSpec, context);
        double ctptyRecovery = c.Double("ctptyRecoveryRate"), investorRecovery = c.Double("investorRecoveryRate");
        JsonElement exposure = c.Input("exposure");
        double vol = exposure.GetProperty("volatility").GetDouble();
        return exposure.GetProperty("kind").GetString() switch
        {
            "blackVol" or "blackQuote" => CounterpartyAdjSwapEngine.Calculate(
                swap, context, discount, vol, ctpty, ctptyRecovery, investor, investorRecovery),
            "bachelier" => CounterpartyAdjSwapEngine.Calculate(
                swap,
                context,
                discount,
                (s, ctx) => BachelierSwaptionEngine.Calculate(s, ctx, discount, vol),
                ctpty,
                ctptyRecovery,
                investor,
                investorRecovery),
            string kind => throw new InvalidOperationException($"unknown exposure model '{kind}'"),
            null => throw new InvalidOperationException("no exposure model"),
        };
    }
}
