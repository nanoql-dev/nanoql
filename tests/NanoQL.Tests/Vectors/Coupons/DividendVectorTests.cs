// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Collections.Immutable;
using NanoQL.Coupons;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Dividend vectors, <c>tests/vectors/cashflows/dividend.json</c>: fixed and fractional dividends' dates, amounts
/// and occurrence, and DividendVector's schedules and refusal.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class DividendVectorTests
{
    [Fact]
    public void DividendVector()
    {
        var file = VectorFile.Load("cashflows/dividend");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            if (c.Inputs.TryGetProperty("amounts", out _))
            {
                if (c.Expected.TryGetProperty("build", out _))
                {
                    check.Output(c, "build", Lazy(() => DividendFunctions.DividendVector(c.DateInputs("dates"), c.Doubles("amounts")).Length));
                    continue;
                }

                ImmutableArray<Dividend> schedule = DividendFunctions.DividendVector(c.DateInputs("dates"), c.Doubles("amounts"));
                check.Output(c, "dates", schedule.Select(d => (object?)d.Date).ToArray());
                check.Output(c, "amounts", schedule.Select(d => (object?)d.Amount(context)).ToArray());
                continue;
            }

            Dividend dividend = c.Inputs.TryGetProperty("rate", out _)
                ? new FractionalDividend(c.Double("rate"), c.DateInput("date"))
                : new FixedDividend(c.Double("amount"), c.DateInput("date"));
            check.Output(c, "date", dividend.Date);
            check.Output(c, "amount", Lazy(() => dividend.Amount(context)));
            check.Output(c, "hasOccurred", dividend.HasOccurred(context));
            if (c.Expected.TryGetProperty("hasOccurredTomorrow", out _))
            {
                check.Output(c, "hasOccurredTomorrow", dividend.HasOccurred(context, context.EvaluationDate + 1));
            }
        }

        check.Complete();
    }
}
