// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The asset swap vectors, <c>tests/vectors/instruments/assetswap.json</c>: par and market asset swaps on eight bonds,
/// paying or receiving the coupons, with a market price, a given floating schedule, day count, gearing, repayment or
/// deal maturity, an overnight index, an engine's settlement and NPV dates, and a seasoned deal; their NPVs, fair
/// spreads and clean prices, the legs' NPVs, BPS, discounts and flows, priced by <see cref="DiscountingSwapEngine"/>'s
/// asset swap overload on the case's curve. A null start or end discount is refused, as the wheel's accessor refuses
/// QuantLib's null one.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class AssetSwapVectorTests
{
    [Fact]
    public void AssetSwapVector()
    {
        var file = VectorFile.Load("instruments/assetswap");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            Bond bond = BondInputs.BondOf(c.Input("bond"), context);
            YieldTermStructure curve = CashFlowInputs.CurveOf(c.Input("curve"));
            AssetSwap swap = SwapOf(c.Input("swap"), bond, context);
            JsonElement engine = c.Input("engine");
            AssetSwapResults results = DiscountingSwapEngine.Calculate(
                swap,
                context,
                curve,
                engine.GetProperty("includeSettlementDateFlows").ValueKind == JsonValueKind.Null
                    ? null
                    : engine.GetProperty("includeSettlementDateFlows").GetBoolean(),
                CashFlowInputs.NullableDate(engine.GetProperty("settlementDate")),
                CashFlowInputs.NullableDate(engine.GetProperty("npvDate")));

            // assetswap.py leaves out the outputs that read a repayment given on a face other than 100 (the S-cms
            // plan's CMR5 (xix)).
            if (c.Expected.TryGetProperty("NPV", out _))
            {
                check.Output(c, "NPV", results.NPV);
                check.Output(c, "fairSpread", () => AssetSwap.FairSpread(results));
                check.Output(c, "fairCleanPrice", () => AssetSwap.FairCleanPrice(results));
                check.Output(c, "legNPV", results.LegNPV);
                check.Output(c, "bondLegAmounts", swap.BondLeg.Select(cf => cf.Amount(context)));
            }

            check.Output(c, "legBPS", results.LegBPS);
            check.Output(c, "startDiscounts", Refused(results.StartDiscounts));
            check.Output(c, "endDiscounts", Refused(results.EndDiscounts));
            check.Output(c, "npvDateDiscount", results.NpvDateDiscount);
            check.Output(c, "bondLegDates", swap.BondLeg.Select(cf => cf.Date));
            check.Output(c, "floatingLegDates", swap.FloatingLeg.Select(cf => cf.Date));
            check.Output(c, "floatingLegAmounts", swap.FloatingLeg.Select(cf => cf.Amount(context)));
        }

        check.Complete();
    }

    /// <summary>The asset swap a spec describes, on a bond.</summary>
    /// <param name="spec">The spec.</param>
    /// <param name="bond">The bond.</param>
    /// <param name="context">The case's context.</param>
    /// <returns>The swap.</returns>
    internal static AssetSwap SwapOf(JsonElement spec, Bond bond, EvaluationContext context)
    {
        JsonElement schedule = spec.GetProperty("floatSchedule");
        JsonElement dayCount = spec.GetProperty("floatingDayCount");
        JsonElement repayment = spec.GetProperty("nonParRepayment");
        return new AssetSwap(
            spec.GetProperty("payBondCoupon").GetBoolean(),
            bond,
            spec.GetProperty("cleanPrice").GetDouble(),
            IndexInputs.IborIndexOf(spec.GetProperty("index")),
            spec.GetProperty("spread").GetDouble(),
            context,
            schedule.ValueKind == JsonValueKind.Null ? null : BondInputs.ScheduleOf(schedule, context),
            dayCount.ValueKind == JsonValueKind.Null ? null : CashFlowInputs.DayCounterOf(dayCount),
            spec.GetProperty("parSwap").GetBoolean(),
            spec.GetProperty("gearing").GetDouble(),
            repayment.ValueKind == JsonValueKind.Null ? null : repayment.GetDouble(),
            CashFlowInputs.NullableDate(spec.GetProperty("dealMaturity")));
    }

    private static Func<object?>[] Refused(ImmutableArray<double?> discounts) =>
        [.. discounts.Select(d => (Func<object?>)(() => d ?? throw new NanoQLException("null discount")))];
}
