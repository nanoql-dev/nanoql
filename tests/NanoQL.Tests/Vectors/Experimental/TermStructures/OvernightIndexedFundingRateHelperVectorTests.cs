// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The overnight-indexed funding rate helper's vectors,
/// <c>tests/vectors/experimental/termstructures/overnightindexedfundingratehelper.json</c>: helpers bootstrapped into a
/// log-linear discount curve, their dates, implied quotes, the curve at their pillars and their swaps' legs.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class OvernightIndexedFundingRateHelperVectorTests
{
    [Fact]
    public void OvernightIndexedFundingRateHelperVector()
    {
        var file = VectorFile.Load("experimental/termstructures/overnightindexedfundingratehelper");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            Date today = context.EvaluationDate;
            OvernightIndexedFundingRateHelper[] helpers;
            if (c.Name.StartsWith("dated", StringComparison.Ordinal))
            {
                helpers =
                [
                    new OvernightIndexedFundingRateHelper(
                        0.003, new Date(c.Int("start")), new Date(c.Int("end")), new UnitedStates(UnitedStatesMarket.Settlement),
                        BusinessDayConvention.ModifiedFollowing, false, new Sofr(Flat(today, 0.041)), 3 * TimeUnit.Months,
                        new Actual360(), context, 2, true),
                ];
            }
            else if (c.Name.Contains("AONIA", StringComparison.Ordinal))
            {
                helpers =
                [
                    new OvernightIndexedFundingRateHelper(
                        0.0004, 1 * TimeUnit.Days, 0, new Australia(), BusinessDayConvention.Following, false,
                        new Aonia(Flat(today, 0.04)), 12 * TimeUnit.Months, new Actual365Fixed(), context, 0, true),
                ];
            }
            else
            {
                var sofr = new Sofr(Flat(today, 0.04));
                helpers =
                [
                    .. c.Input("quotes").EnumerateArray().Select(q => new OvernightIndexedFundingRateHelper(
                        q[1].GetDouble(), new Period(q[0][0].GetInt32(), (TimeUnit)q[0][1].GetInt32()), 2,
                        new UnitedStates(UnitedStatesMarket.Settlement), BusinessDayConvention.ModifiedFollowing, false, sofr,
                        3 * TimeUnit.Months, new Actual360(), context, 2, true)),
                ];
            }

            var curve = new PiecewiseYieldCurve<Discount, LogLinear, IterativeBootstrap>(today, helpers, new Actual365Fixed(), context);
            OvernightIndexedFundingRateHelper[] bound = [.. helpers.Select(h => h.SetTermStructure(curve))];
            check.Output(c, "earliestDates", bound.Select(h => (object?)h.EarliestDate).ToArray());
            check.Output(c, "latestDates", bound.Select(h => (object?)h.LatestDate).ToArray());
            check.Output(c, "latestRelevantDates", bound.Select(h => (object?)h.LatestRelevantDate).ToArray());
            check.Output(c, "pillarDates", bound.Select(h => (object?)h.PillarDate).ToArray());
            check.Output(c, "maturityDates", bound.Select(h => (object?)h.MaturityDate).ToArray());
            check.Output(c, "impliedQuotes", bound.Select(h => h.ImpliedQuote(context)).ToArray());
            check.Output(c, "pillarDiscounts", bound.Select(h => curve.Discount(h.PillarDate!.Value)).ToArray());
            foreach (int j in new[] { 0, 1 })
            {
                check.Output(c, $"leg{j}Dates", bound.SelectMany(h => h.Swap.Leg(j)).Select(cf => (object?)cf.Date).ToArray());
                check.Output(c, $"leg{j}Amounts", bound.SelectMany(h => h.Swap.Leg(j)).Select(cf => cf.Amount(context)).ToArray());
            }
        }

        check.Complete();
    }

    private static YieldTermStructure Flat(Date today, double rate) =>
        new FlatForward(today, rate, new Actual365Fixed(), Compounding.Continuous);
}
