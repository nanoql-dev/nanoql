// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's irregular swaption vectors, <c>tests/vectors/cxx/experimental/swaptions/irregularswaption.json</c>:
/// irregular swaps on the discounting swap engine and their swaptions on Hagan's engine, with the swaptions' types and
/// settlement types, and an expired swap and swaption.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class IrregularSwaptionCxxVectorTests
{
    [Fact]
    public void IrregularSwaptionCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/swaptions/irregularswaption");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            JsonElement[] a = [.. c.Input("args").EnumerateArray()];
            var reference = new Date(a[0].GetInt32());
            EvaluationContext context = c.Context();
            YieldTermStructure curve = new FlatForward(reference, a[1].GetDouble(), new Actual365Fixed());
            var index = new Euribor6M(curve);
            Date start = new TARGET().Advance(reference, 1, TimeUnit.Years);
            Date end = start + (5 * TimeUnit.Years);
            Schedule fixedSchedule = Schedule(start, end, 1 * TimeUnit.Years);
            Schedule floatSchedule = Schedule(start, end, 6 * TimeUnit.Months);
            ImmutableArray<CashFlow> fixedLeg = new FixedRateLeg(fixedSchedule)
                .WithNotionals(Numbers(a[3]))
                .WithCouponRates(Numbers(a[4]), new Thirty360(Thirty360Convention.BondBasis))
                .ToLeg();
            ImmutableArray<CashFlow> floatLeg = new IborLeg(floatSchedule, index)
                .WithNotionals(Numbers(a[5]))
                .WithSpreads(Numbers(a[6]))
                .ToLeg(context);
            var swap = new IrregularSwap(a[2].GetInt32() > 0 ? SwapType.Payer : SwapType.Receiver, fixedLeg, floatLeg);
            var results = (IrregularSwapResults)DiscountingSwapEngine.Calculate(swap, context, curve);
            check.Output(c, "npv", results.NPV!.Value);
            check.Output(c, "legNPV", new[] { results.LegNPV[0]!.Value, results.LegNPV[1]!.Value });
            check.Output(c, "legBPS", new[] { results.LegBPS[0]!.Value, results.LegBPS[1]!.Value });
            check.Output(c, "fixedLegNPV", results.FixedLegNPV!.Value);
            check.Output(c, "floatingLegNPV", results.FloatingLegNPV!.Value);
            var vol = new ConstantSwaptionVolatility(
                reference, new TARGET(), BusinessDayConvention.ModifiedFollowing, a[7].GetDouble(), new Actual365Fixed(),
                VolatilityType.Normal);
            var swaption = new IrregularSwaption(swap, new EuropeanExercise(start));
            check.Output(c, "hagan", HaganIrregularSwaptionEngine.Calculate(swaption, context, vol, curve).NPV!.Value);
            check.Output(c, "type", (int)swaption.Type);
            check.Output(c, "settlementType", IrregularSwaptionFunctions.ToString(swaption.SettlementType));
            check.Output(c, "settlementTypes", new[]
            {
                IrregularSwaptionFunctions.ToString(IrregularSettlementType.Physical),
                IrregularSwaptionFunctions.ToString(IrregularSettlementType.Cash),
            });
        }

        check.Complete();
    }

    private static double[] Numbers(JsonElement e) => [.. e.EnumerateArray().Select(x => x.GetDouble())];

    private static Schedule Schedule(Date start, Date end, Period tenor) =>
        new MakeSchedule()
            .From(start)
            .To(end)
            .WithTenor(tenor)
            .WithCalendar(new TARGET())
            .WithConvention(BusinessDayConvention.ModifiedFollowing)
            .Backwards()
            .ToSchedule();
}
