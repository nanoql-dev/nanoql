// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Coupons;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The multiple-resets swap rate helper vectors,
/// <c>tests/vectors/termstructures/yield/multipleresetsswaphelper.json</c>:
/// <see cref="MultipleResetsSwapRateHelper"/>'s dates and swap, and log-linear discount curves bootstrapped on one
/// helper and on five, with the helpers' implied quotes on them.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MultipleResetsSwapHelperVectorTests
{
    [Fact]
    public void MultipleResetsSwapHelperVector()
    {
        var file = VectorFile.Load("termstructures/yield/multipleresetsswaphelper");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            MultipleResetsSwapRateHelper[] helpers = [.. c.Input("helpers").EnumerateArray().Select(s => HelperOf(s, context))];
            if (c.String("kind") == "helper")
            {
                MultipleResetsSwapRateHelper helper = helpers[0];
                check.Output(c, "earliestDate", helper.EarliestDate);
                check.Output(c, "latestDate", helper.LatestDate);
                check.Output(c, "maturityDate", helper.MaturityDate);
                check.Output(c, "pillarDate", helper.PillarDate);
                check.Output(c, "latestRelevantDate", helper.LatestRelevantDate);
                check.Output(c, "swapStartDate", helper.Swap.StartDate());
                check.Output(c, "swapMaturityDate", helper.Swap.MaturityDate());
                check.Output(c, "swapFixedRate", helper.Swap.FixedRate);

                PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>? curve = null;
                Exception? error = null;
                try
                {
                    curve = new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(
                        context.EvaluationDate, [helper], new Actual365Fixed(), context);
                }
                catch (NanoQLException e)
                {
                    error = e;
                }

                // a curve that can't be bootstrapped refuses each output, as QuantLib's lazy curve does
                check.Output(c, "bootstrapDiscount", () => (curve ?? throw error!).Discount(helper.LatestDate!.Value));
                check.Output(c, "impliedQuote", () => helper.SetTermStructure(curve ?? throw error!).ImpliedQuote(context));
                check.Output(c, "quoteError", () => helper.SetTermStructure(curve ?? throw error!).QuoteError(context));
                continue;
            }

            var bootstrapped = new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(
                context.EvaluationDate, helpers, new Actual365Fixed(), context);
            check.Output(c, "discounts", helpers.Select(h => (Func<object?>)(() => bootstrapped.Discount(h.LatestDate!.Value))).ToArray());
            check.Output(
                c,
                "impliedQuotes",
                helpers.Select(h => (Func<object?>)(() => h.SetTermStructure(bootstrapped).ImpliedQuote(context))).ToArray());
        }

        check.Complete();
    }

    // The helper a spec describes, a null curve none and a null day count the index's.
    private static MultipleResetsSwapRateHelper HelperOf(JsonElement spec, EvaluationContext context)
    {
        JsonElement tenor = spec.GetProperty("tenor");
        JsonElement curve = spec.GetProperty("discountingCurve");
        JsonElement dc = spec.GetProperty("fixedDayCount");
        return new MultipleResetsSwapRateHelper(
            spec.GetProperty("settlementDays").GetInt32(),
            new Period(tenor[0].GetInt32(), (TimeUnit)tenor[1].GetInt32()),
            spec.GetProperty("fixedRate").GetDouble(),
            IndexInputs.IborIndexOf(spec.GetProperty("index")),
            spec.GetProperty("resetsPerCoupon").GetInt32(),
            context,
            curve.ValueKind == JsonValueKind.Null ? null : IndexInputs.CurveOf(curve),
            spec.GetProperty("averaging").GetString() == "averaging" ? RateAveragingType.Simple : RateAveragingType.Compound,
            spec.GetProperty("spread").GetDouble(),
            (Frequency)spec.GetProperty("fixedFrequency").GetInt32(),
            dc.ValueKind == JsonValueKind.Null ? null : CashFlowInputs.DayCounterOf(dc),
            (BusinessDayConvention)spec.GetProperty("fixedConvention").GetInt32());
    }
}
