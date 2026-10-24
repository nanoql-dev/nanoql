// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The asset swap's harness vectors, <c>tests/vectors/cxx/instruments/assetswap.json</c>: the members the wheel hides,
/// the fair non-par repayment, the floating leg's NPV and BPS and the swap's terms, on par and market swaps on
/// fixed-rate bonds priced by <see cref="DiscountingSwapEngine"/>'s asset swap overload on the case's curve.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AssetSwapCxxVectorTests
{
    [Fact]
    public void AssetSwapCxxVector()
    {
        var file = VectorFile.Load("cxx/instruments/assetswap");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement[] a = [.. c.Input("arguments").EnumerateArray()];
            Bond bond = BondInputs.BondOf(a[0], context);
            YieldTermStructure curve = CashFlowInputs.CurveOf(a[1]);
            IborIndex index = IndexInputs.IborIndexOf(a[4]);
            var swap = new AssetSwap(
                a[2].GetBoolean(),
                bond,
                a[3].GetDouble(),
                index,
                a[5].GetDouble(),
                context,
                null,
                index.DayCounter,
                a[6].GetBoolean(),
                1.0,
                a[7].ValueKind == JsonValueKind.Null ? null : a[7].GetDouble());
            AssetSwapResults results = DiscountingSwapEngine.Calculate(swap, context, curve);

            // cxx/instruments/assetswap.py leaves out the repayments on a bond redeemed above par with none given
            // (the S-cms plan's CMR5 (xx))
            if (c.Expected.TryGetProperty("nonParRepayment", out _))
            {
                check.Output(c, "fairNonParRepayment", () => AssetSwap.FairNonParRepayment(results));
                check.Output(c, "nonParRepayment", swap.NonParRepayment);
            }

            check.Output(c, "floatingLegNPV", () => AssetSwap.FloatingLegNPV(results));
            check.Output(c, "floatingLegBPS", () => AssetSwap.FloatingLegBPS(results));
            check.Output(c, "cleanPrice", swap.CleanPrice);
            check.Output(c, "spread", swap.Spread);
            check.Output(c, "parSwap", swap.ParSwap);
            check.Output(c, "payBondCoupon", swap.PayBondCoupon);
            check.Output(c, "bondSettlementDate", swap.Bond.SettlementDate(context));
        }

        check.Complete();
    }
}
