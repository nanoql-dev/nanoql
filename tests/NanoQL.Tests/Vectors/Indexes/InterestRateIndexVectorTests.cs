// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The interest-rate index vectors, <c>tests/vectors/indexes/interestrateindex.json</c>: Ibor and overnight indexes'
/// names, tenors, family names and fixing days, and their fixing, value and maturity dates, fixing-date validity and
/// fixings at past, present and future dates, with fixings stored in the case's context and today's historic fixings
/// enforced or not.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class InterestRateIndexVectorTests
{
    [Fact]
    public void InterestRateIndexVector()
    {
        var file = VectorFile.Load("indexes/interestrateindex");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            InterestRateIndex index = IndexInputs.IborIndexOf(c.Input("index"));
            Date[] dates = c.DateInputs("dates");
            if (c.Expected.TryGetProperty("name", out _))
            {
                check.Output(c, "name", index.Name);
                check.Output(c, "tenor", new object[] { index.Tenor.Length, (int)index.Tenor.Units });
                check.Output(c, "familyName", index.FamilyName);
                check.Output(c, "fixingDays", index.FixingDays);
                check.Output(c, "fixingDate", Each(dates, index.FixingDate));
                check.Output(c, "valueDate", Each(dates, index.ValueDate));
                check.Output(c, "maturityDate", Each(dates, index.MaturityDate));
                check.Output(c, "isValidFixingDate", Each(dates, index.IsValidFixingDate));
            }

            check.Output(c, "fixing", Each(dates, d => index.Fixing(d, context)));
            check.Output(c, "forecastTodaysFixing", () => index.Fixing(context.EvaluationDate, context, true));
        }

        check.Complete();
    }

    private static Func<object?>[] Each<TOut>(Date[] dates, Func<Date, TOut> f) =>
        dates.Select(d => (Func<object?>)(() => f(d))).ToArray();
}
