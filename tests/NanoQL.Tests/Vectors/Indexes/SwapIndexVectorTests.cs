// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The swap-index vectors, <c>tests/vectors/indexes/swapindex.json</c>: <see cref="SwapIndex"/> and
/// <see cref="OvernightIndexedSwapIndex"/>, their conventions, dates and fixings under both at-par settings (the
/// overnight-indexed ones forecast from the base's vanilla swap, as QuantLib's do), their three clones, and an
/// overnight-indexed swap index's own underlying swap, priced as QuantLib's builder prices it.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SwapIndexVectorTests
{
    [Fact]
    public void SwapIndexVector()
    {
        var file = VectorFile.Load("indexes/swapindex");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            SwapIndex index = SwapIndexInputs.SwapIndexOf(c.Input("index"));
            Date[] dates = c.DateInputs("dates");
            check.Output(c, "fixing", Each(dates, d => index.Fixing(d, context)));
            if (!c.Inputs.TryGetProperty("cloneDates", out _))
            {
                continue;
            }

            Date[] cloneDates = c.DateInputs("cloneDates");

            check.Output(c, "name", index.Name);
            check.Output(c, "familyName", index.FamilyName);
            check.Output(c, "tenor", new object[] { index.Tenor.Length, index.Tenor.Units });
            check.Output(c, "fixingDays", index.FixingDays);
            check.Output(c, "fixedLegTenor", new object[] { index.FixedLegTenor.Length, index.FixedLegTenor.Units });
            check.Output(c, "fixedLegConvention", index.FixedLegConvention);
            check.Output(c, "dayCounter", index.DayCounter.Name);
            check.Output(c, "fixingCalendar", index.FixingCalendar.Name);
            check.Output(c, "iborIndex", index.IborIndex.Name);
            check.Output(c, "forwardingTermStructureEmpty", index.ForwardingTermStructure is null);
            check.Output(c, "discountingTermStructureEmpty", index.DiscountingTermStructure is null);
            check.Output(c, "fixingDate", Each(dates, index.FixingDate));
            check.Output(c, "valueDate", Each(dates, index.ValueDate));
            check.Output(c, "maturityDate", Each(dates, index.MaturityDate));

            YieldTermStructure? forwarding = IndexInputs.CurveOf(c.Input("cloneForwarding"));
            YieldTermStructure? discounting = IndexInputs.CurveOf(c.Input("cloneDiscounting"));
            SwapIndex[] clones =
            [
                index.Clone(forwarding),
                index.Clone(forwarding, discounting),
                index.Clone(new Period(3, TimeUnit.Years)),
            ];
            check.Output(c, "cloneNames", clones.Select(x => x.Name));
            check.Output(c, "cloneTypes", clones.Select(x => x.GetType().Name));
            check.Output(
                c,
                "cloneFixings",
                clones.SelectMany(x => cloneDates.Select(d => (Func<object?>)(() => x.Fixing(d, context)))).ToArray());

            if (index is OvernightIndexedSwapIndex ois)
            {
                OvernightIndexedSwap[] swaps = [.. c.DateInputs("swapDates").Select(d => ois.UnderlyingSwap(d, context))];
                YieldTermStructure curve = ois.OvernightIndex.ForwardingTermStructure!;
                check.Output(c, "overnightIndex", ois.OvernightIndex.Name);
                check.Output(c, "underlyingStartDate", swaps.Select(s => (object)s.StartDate()));
                check.Output(c, "underlyingMaturityDate", swaps.Select(s => (object)s.MaturityDate()));

                // MakeOIS's default engine: on the overnight index's curve, without the settlement date's flows.
                check.Output(
                    c,
                    "underlyingFairRate",
                    swaps.Select(s => (Func<object?>)(() =>
                        DiscountingSwapEngine.Calculate(s, context, curve, includeSettlementDateFlows: false).FairRate))
                    .ToArray());
            }
        }

        check.Complete();
    }

    private static Func<object?>[] Each<TOut>(Date[] dates, Func<Date, TOut> f) =>
        dates.Select(d => (Func<object?>)(() => f(d))).ToArray();
}
