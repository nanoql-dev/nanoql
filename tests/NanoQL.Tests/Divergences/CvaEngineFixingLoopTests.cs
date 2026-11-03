// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using System.Text.Json.Nodes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Tests.Vectors;
using NanoQL.Time;
using static NanoQL.Time.Month;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The CVA engine's loop over the fixed payment dates: QuantLib's <c>CounterpartyAdjSwapEngine</c> skips the dates
/// before the counterparty curve's reference date with <c>while (*nextFD &lt; priceDate) ++nextFD;</c>
/// (<c>cvaswapengine.cpp:128</c>), with no end check, so when every fixed payment is before that date it reads past
/// the dates, undefined behaviour. nanoql's loop stops at the end, and the swap is then worth its NPV, its fair rate
/// the swap's. QuantLib's checks: its values where its loop stays in range (the C++ harness's vectors, a seasoned
/// swap among them); and a seasoned swap against a fresh one with the same remaining flows, which QuantLib's loop
/// prices alike, the seasoned swap skipping its past payments: the vectors' seasoned 5-year payer from 16 April 2022
/// at its fair rate, -1,880.3521932368576 and 0.025867936451198614, restarted on 16 April 2024.
/// </summary>
public class CvaEngineFixingLoopTests
{
    [Fact]
    public void ASeasonedSwapSkipsItsPastPayments()
    {
        VectorCase c = Case("seasoned 5Y payer");
        EvaluationContext context = c.Context();
        JsonNode spec = JsonNode.Parse(c.Input("swap").GetRawText())!;
        int restart = new Date(16, April, 2024).SerialNumber;
        spec["fixedSchedule"]!["effectiveDate"] = restart;
        spec["floatSchedule"]!["effectiveDate"] = restart;
        VanillaSwap fresh = SwapInputs.VanillaSwapOf(JsonSerializer.SerializeToElement(spec), context);
        VanillaSwap seasoned = SwapInputs.VanillaSwapOf(c.Input("swap"), context);
        Assert.True(fresh.FixedLeg.Length < seasoned.FixedLeg.Length);

        FixedVsFloatingSwapResults results = Price(fresh, c, context);
        Assert.True(Math.Abs(results.NPV!.Value - -1880.3521932368576) <= 1e-7, $"{results.NPV}");
        Assert.True(Math.Abs(results.FairRate!.Value - 0.025867936451198614) <= 1e-15, $"{results.FairRate}");
    }

    [Fact]
    public void EveryPaymentBeforeThePriceDateLeavesTheSwapsValue()
    {
        VectorCase c = Case("5Y payer, Black, no investor curve");
        EvaluationContext context = c.Context();
        VanillaSwap swap = SwapInputs.VanillaSwapOf(c.Input("swap"), context);
        YieldTermStructure discount = IndexInputs.CurveOf(c.Input("discountCurve"))!;
        Date last = swap.FixedLeg[^1].Date;
        var ctpty = new FlatHazardRate(last + 1, 0.02, new Actual365Fixed());

        FixedVsFloatingSwapResults adjusted = CounterpartyAdjSwapEngine.Calculate(
            swap, context, discount, 0.15, ctpty, c.Double("ctptyRecoveryRate"));
        FixedVsFloatingSwapResults plain = DiscountingSwapEngine.Calculate(swap, context, discount);
        Assert.Equal(plain.NPV, adjusted.NPV);
        Assert.Equal(plain.FairRate!.Value, adjusted.FairRate!.Value, 1e-15);
    }

    private static VectorCase Case(string name) =>
        VectorFile.Load("cxx/pricingengines/swap/cvaswapengine").Cases.Single(x => x.Name == name);

    private static FixedVsFloatingSwapResults Price(VanillaSwap swap, VectorCase c, EvaluationContext context) =>
        CounterpartyAdjSwapEngine.Calculate(
            swap,
            context,
            IndexInputs.CurveOf(c.Input("discountCurve"))!,
            c.Input("exposure").GetProperty("volatility").GetDouble(),
            CreditInputs.CurveOf(c.Input("ctptyCurve"), context),
            c.Double("ctptyRecoveryRate"),
            null,
            c.Double("investorRecoveryRate"));
}
