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
/// The overnight-index future rate helper vectors,
/// <c>tests/vectors/termstructures/yield/overnightindexfutureratehelper.json</c>: generic and SOFR helpers, their
/// quotes, adjustments and dates, and a log-linear discount curve bootstrapped on each alone, its discount at the
/// helper's latest date and the helper's implied quote and error on it; curves bootstrapped on a SOFR strip and on ESTR
/// futures, their discounts and implied quotes; and the helpers' refusals of their pillars and of a SOFR frequency.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class OvernightIndexFutureRateHelperVectorTests
{
    [Fact]
    public void OvernightIndexFutureRateHelperVector()
    {
        var file = VectorFile.Load("termstructures/yield/overnightindexfutureratehelper");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement[] specs = [.. c.Input("helpers").EnumerateArray()];
            if (c.String("kind") == "refused")
            {
                check.Output(c, "construct", () => HelperOf(specs[0]));
                continue;
            }

            OvernightIndexFutureRateHelper[] helpers = [.. specs.Select(HelperOf)];
            if (c.String("kind") == "helper")
            {
                OvernightIndexFutureRateHelper helper = helpers[0];
                check.Output(c, "quote", helper.Quote);
                check.Output(c, "convexityAdjustment", helper.ConvexityAdjustment);
                check.Output(c, "earliestDate", helper.EarliestDate);
                check.Output(c, "latestDate", helper.LatestDate);
                check.Output(c, "maturityDate", helper.MaturityDate);
                check.Output(c, "pillarDate", helper.PillarDate);
                check.Output(c, "latestRelevantDate", helper.LatestRelevantDate);

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

    // The helper a spec describes: the generic helper on its dates and index, or the SOFR one on its reference period.
    private static OvernightIndexFutureRateHelper HelperOf(JsonElement spec)
    {
        double price = spec.GetProperty("price").GetDouble();
        double convexity = spec.GetProperty("convexityAdjustment").GetDouble();
        var pillar = (PillarChoice)spec.GetProperty("pillar").GetInt32();
        Date? custom = CashFlowInputs.NullableDate(spec.GetProperty("customPillarDate"));
        if (spec.GetProperty("type").GetString() == "generic")
        {
            return new OvernightIndexFutureRateHelper(
                price,
                new Date(spec.GetProperty("valueDate").GetInt32()),
                new Date(spec.GetProperty("maturityDate").GetInt32()),
                OvernightInputs.IndexOf(spec.GetProperty("index")),
                convexity,
                (RateAveragingType)spec.GetProperty("averaging").GetInt32(),
                pillar,
                custom);
        }

        return new SofrFutureRateHelper(
            price,
            (Month)spec.GetProperty("month").GetInt32(),
            spec.GetProperty("year").GetInt32(),
            (Frequency)spec.GetProperty("frequency").GetInt32(),
            convexity,
            pillar,
            custom);
    }
}
