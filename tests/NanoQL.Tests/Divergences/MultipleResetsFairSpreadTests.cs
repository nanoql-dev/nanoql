// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Tests.Vectors;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The compounded multiple-resets swap's fair spread: QuantLib's <c>FixedVsFloatingSwap::fairSpread</c> is the spread
/// less the NPV over the floating leg's BPS (<c>fixedvsfloatingswap.cpp</c>), linear in the spread, while on a
/// compounding leg the spread compounds with each fixing, so at it the swap misses par: on the wheel, on the vectors'
/// 5-year payer of monthly resets compounded quarterly on 1e7, QuantLib's fair spread from a spread of 0 is
/// −0.00044495679505344586, where the swap is worth −1.078e-5 a unit of nominal, and from 2% −0.000582486889730293,
/// worth −6.618e-4. nanoql's is the spread at which the swap is worth zero, solved from QuantLib's estimate; on an
/// averaging leg the spread is linear, and QuantLib's formula stands. QuantLib's checks: its NPV at the solved spread,
/// the root of its own swap's NPV, −0.00044267984438780547, within 1.5e-8 of par on 1e7 (a secant on the wheel); and on
/// the vectors' averaged swap, its fair spread, −0.001341481056075899, bit for bit, where its swap is worth −4.7e-10.
/// </summary>
public class MultipleResetsFairSpreadTests
{
    private const double Root = -0.00044267984438780547;

    [Fact]
    public void TheCompoundedFairSpreadPricesAtPar()
    {
        VectorCase c = Case("swap 5Y, monthly resets compounded quarterly, payer /");
        EvaluationContext context = c.Context();
        YieldTermStructure discount = IndexInputs.CurveOf(c.Input("discountCurve"))!;
        foreach (double spread in new[] { 0.0, 0.02 })
        {
            MultipleResetsSwap swap = MultipleResetsSwapVectorTests.SwapOf(WithSpread(c.Input("swap"), spread), context);
            double fair = DiscountingSwapEngine.Calculate(swap, context, discount).FairSpread!.Value;
            Assert.True(Math.Abs(fair - Root) <= 1e-12, $"{fair} {Root}");

            MultipleResetsSwap atFair = MultipleResetsSwapVectorTests.SwapOf(WithSpread(c.Input("swap"), fair), context);
            double npv = DiscountingSwapEngine.Calculate(atFair, context, discount).NPV!.Value;
            // Brent's accuracy in the spread, 1e-12, is about 5e-5 in the NPV
            Assert.True(Math.Abs(npv) <= 1e-6, $"{npv}");
        }
    }

    [Fact]
    public void TheAveragedFairSpreadIsQuantLibs()
    {
        VectorCase c = Case("swap 5Y, monthly resets averaged quarterly, receiver, a spread /");
        EvaluationContext context = c.Context();
        YieldTermStructure discount = IndexInputs.CurveOf(c.Input("discountCurve"))!;
        MultipleResetsSwap swap = MultipleResetsSwapVectorTests.SwapOf(c.Input("swap"), context);
        Assert.Equal(-0.001341481056075899, DiscountingSwapEngine.Calculate(swap, context, discount).FairSpread);
    }

    private static VectorCase Case(string prefix) =>
        VectorFile.Load("instruments/multipleresetsswap").Cases.Single(x => x.Name.StartsWith(prefix, StringComparison.Ordinal));

    private static JsonElement WithSpread(JsonElement swap, double spread)
    {
        JsonNode node = JsonNode.Parse(swap.GetRawText())!;
        node["spread"] = spread;
        return JsonSerializer.SerializeToElement(node);
    }
}
