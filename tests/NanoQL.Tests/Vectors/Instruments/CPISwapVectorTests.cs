// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using NanoQL.TermStructures;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The CPI swap vectors, <c>tests/vectors/instruments/cpiswap.json</c>: <see cref="CPISwap"/>'s two legs and the
/// discounting engine's results, its fair spread among them; the fair rate, which the engine solves where QuantLib
/// derives it from the CPI leg's BPS, is a divergence's.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class CPISwapVectorTests
{
    [Fact]
    public void CPISwapVector()
    {
        var file = VectorFile.Load("instruments/cpiswap");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            (InflationIndex index, EvaluationContext context) = InflationCashFlowInputs.IndexOf(c);
            CPISwap swap = SwapOf(c, (ZeroInflationIndex)index, context);
            YieldTermStructure nominal = CashFlowInputs.CurveOf(c.Input("nominalCurve"));
            CPISwapResults results = DiscountingSwapEngine.Calculate(swap, context, nominal);
            check.Output(c, "cpiDates", swap.CpiLeg.Select(f => f.Date));
            check.Output(c, "cpiAmounts", swap.CpiLeg.Select(f => (Func<object?>)(() => f.Amount(context))).ToArray());
            check.Output(c, "floatDates", swap.FloatLeg.Select(f => f.Date));
            check.Output(c, "floatAmounts", swap.FloatLeg.Select(f => (Func<object?>)(() => f.Amount(context))).ToArray());
            check.Output(c, "NPV", results.NPV);
            check.Output(c, "legNPV", results.LegNPV.Select(x => (object?)x));
            check.Output(c, "legBPS", results.LegBPS.Select(x => (object?)x));
            check.Output(c, "fixedLegNPV", results.FixedLegNPV);
            check.Output(c, "floatLegNPV", results.FloatLegNPV);
            check.Output(c, "fairSpread", results.FairSpread);
        }

        check.Complete();
    }

    /// <summary>The swap a case's arguments describe.</summary>
    /// <param name="c">The case.</param>
    /// <param name="index">The zero-inflation index.</param>
    /// <param name="context">The case's evaluation context.</param>
    /// <returns>The swap.</returns>
    internal static CPISwap SwapOf(VectorCase c, ZeroInflationIndex index, EvaluationContext context)
    {
        JsonElement floatIndex = c.Input("floatIndex");
        return new CPISwap(
            (SwapType)c.Int("type"),
            c.Double("nominal"),
            c.Bool("subtractInflationNominal"),
            c.Double("spread"),
            CashFlowInputs.DayCounterOf(c.Input("floatDayCount")),
            CPICouponVectorTests.ScheduleOf(c.Input("floatSchedule"), context),
            (BusinessDayConvention)c.Int("floatRoll"),
            c.Int("fixingDays"),
            floatIndex.ValueKind == JsonValueKind.Null ? null : IndexInputs.IborIndexOf(floatIndex),
            c.Double("fixedRate"),
            InflationCashFlowInputs.NullableDoubleOf(c.Input("baseCPI")),
            CashFlowInputs.DayCounterOf(c.Input("fixedDayCount")),
            CPICouponVectorTests.ScheduleOf(c.Input("fixedSchedule"), context),
            (BusinessDayConvention)c.Int("fixedRoll"),
            InflationCashFlowInputs.PeriodOf(c.Input("observationLag")),
            index,
            context,
            (CPIInterpolationType)c.Int("interpolation"),
            InflationCashFlowInputs.NullableDoubleOf(c.Input("inflationNominal")));
    }
}
