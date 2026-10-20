// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Coupons;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The dividends' harness vectors, <c>tests/vectors/cxx/cashflows/dividend.json</c>: a fractional dividend's rate,
/// nominal, date and amounts, with and without a nominal, and a fixed dividend's amount on an underlying value.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class DividendCxxVectorTests
{
    [Fact]
    public void DividendCxxVector()
    {
        var file = VectorFile.Load("cxx/cashflows/dividend");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            double[] underlyings = c.Doubles("underlyings");
            if (c.Inputs.TryGetProperty("amount", out _))
            {
                var fixedDividend = new FixedDividend(c.Double("amount"), c.DateInput("date"));
                check.Output(c, "amountOn", underlyings.Select(u => (object?)fixedDividend.Amount(u)).ToArray());
                continue;
            }

            FractionalDividend d = c.Inputs.GetProperty("nominal").ValueKind == System.Text.Json.JsonValueKind.Null
                ? new FractionalDividend(c.Double("rate"), c.DateInput("date"))
                : new FractionalDividend(c.Double("rate"), c.Double("nominal"), c.DateInput("date"));
            check.Output(c, "rate", d.Rate);
            check.Output(c, "nominal", d.Nominal);
            check.Output(c, "date", d.Date);
            check.Output(c, "amount", Lazy(() => d.Amount(context)));
            check.Output(c, "amountOn", underlyings.Select(u => (object?)d.Amount(u)).ToArray());
        }

        check.Complete();
    }
}
