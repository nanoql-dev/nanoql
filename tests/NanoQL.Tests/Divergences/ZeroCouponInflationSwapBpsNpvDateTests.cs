// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Tests.Vectors;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// <see cref="ZeroCouponInflationSwap.FixedLegBPS"/> at an NPV date: QuantLib's <c>fixedLegBPS</c> discounts the fixed
/// flow's move by the fixed leg's end discount, read from the curve's reference date
/// (<c>zerocouponinflationswap.cpp:147-154</c>), while the engine gives the leg NPVs at its NPV date, so with a later
/// NPV date the BPS isn't the fixed leg's NPV move. On the wheel, on the vectors' first swap (a payer of 1,000,000 at
/// 3.2% to 15 June 2029), QuantLib's BPS is 464.37247408343586 at every NPV date, where the fixed leg's NPV moves by
/// 483.32387426443165 for a basis point at an NPV date a year after the evaluation date and 523.5785024892248 three
/// years after. nanoql's is the end discount over the NPV date's discount, the NPV move; at the default NPV date
/// QuantLib's bits (the vectors). QuantLib's check: its own fixed leg NPVs at the rate and a basis point above it, at
/// the NPV date.
/// </summary>
public class ZeroCouponInflationSwapBpsNpvDateTests
{
    [Theory]
    [InlineData(365, 483.32387426443165)]
    [InlineData(1095, 523.5785024892248)]
    public void AtAnNpvDateTheBpsIsTheFixedLegsMove(int days, double quantLibsMove)
    {
        VectorCase c = VectorFile.Load("instruments/zerocouponinflationswap")
            .Cases.Single(x => x.Name == "swap payer, flat / 0");
        (InflationIndex index, EvaluationContext context) = InflationCashFlowInputs.IndexOf(c);
        YieldTermStructure nominal = CashFlowInputs.CurveOf(c.Input("nominalCurve"));
        Date npvDate = context.EvaluationDate + days;
        double rate = c.Double("fixedRate");
        ZeroCouponInflationSwap swap = ZeroCouponInflationSwapVectorTests.SwapOf(c, (ZeroInflationIndex)index);
        ZeroCouponInflationSwap bumped = ZeroCouponInflationSwapVectorTests.SwapOf(
            c, (ZeroInflationIndex)index, rate + 1e-4);

        SwapResults results = DiscountingSwapEngine.Calculate(swap, context, nominal, npvDate: npvDate);
        double move = ZeroCouponInflationSwap.FixedLegNPV(
                DiscountingSwapEngine.Calculate(bumped, context, nominal, npvDate: npvDate))!.Value
            - ZeroCouponInflationSwap.FixedLegNPV(results)!.Value;

        Assert.True(Math.Abs(move - quantLibsMove) <= 1e-9, $"move {move}");
        double bps = swap.FixedLegBPS(results);
        Assert.True(Math.Abs(bps - move) <= 1e-9, $"BPS {bps}, move {move}");
    }
}
