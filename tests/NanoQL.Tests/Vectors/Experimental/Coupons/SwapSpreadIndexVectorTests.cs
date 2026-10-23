// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Indexes;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The swap spread index vectors, <c>tests/vectors/experimental/coupons/swapspreadindex.json</c>: on four swap index
/// pairs, the name, the conventions it takes from the first index, the gearings, forecasts and fixings, the past
/// fixings with one or both indexes' stored, maturityDate's refusal, and the constructor's six refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SwapSpreadIndexVectorTests
{
    [Fact]
    public void SwapSpreadIndexVector()
    {
        var file = VectorFile.Load("experimental/coupons/swapspreadindex");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            SwapIndex index1 = SwapIndexInputs.SwapIndexOf(c.Input("index1"));
            SwapIndex index2 = SwapIndexInputs.SwapIndexOf(c.Input("index2"));
            string familyName = c.String("familyName");
            double gearing1 = c.Double("gearing1"), gearing2 = c.Double("gearing2");
            if (c.Expected.TryGetProperty("construct", out _))
            {
                check.Output(c, "construct", () => new SwapSpreadIndex(familyName, index1, index2, gearing1, gearing2));
                continue;
            }

            var idx = new SwapSpreadIndex(familyName, index1, index2, gearing1, gearing2);
            Date[] future = c.DateInputs("future");
            Date pastBoth = c.DateInput("pastBoth"), pastFirst = c.DateInput("pastFirst");
            check.Output(c, "name", idx.Name);
            check.Output(c, "familyName", idx.FamilyName);
            check.Output(c, "tenor", new[] { idx.Tenor.Length, (int)idx.Tenor.Units });
            check.Output(c, "fixingDays", idx.FixingDays);
            check.Output(c, "fixingCalendar", idx.FixingCalendar.Name);
            check.Output(c, "currency", idx.Currency?.Code);
            check.Output(c, "dayCounter", idx.DayCounter.Name);
            check.Output(c, "gearing1", idx.Gearing1);
            check.Output(c, "gearing2", idx.Gearing2);
            check.Output(c, "forecastFixing", future.Select(d => idx.ForecastFixing(d, context)).ToArray());
            check.Output(c, "fixing", future.Select(d => idx.Fixing(d, context)).ToArray());
            check.Output(c, "pastFixingBoth", idx.PastFixing(pastBoth, context));
            check.Output(c, "pastFixingFirst", idx.PastFixing(pastFirst, context));
            check.Output(c, "fixingPast", idx.Fixing(pastBoth, context));
            check.Output(c, "maturityDate", () => idx.MaturityDate(future[0]));
        }

        check.Complete();
    }
}
