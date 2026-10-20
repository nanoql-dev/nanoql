// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Numerics;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The QuantoTermStructure vectors, <c>tests/vectors/termstructures/yield/quantotermstructure.json</c>: quanto
/// curves from flat and zero curves and constant, curve and surface volatilities, their dates, conventions, discounts
/// and rates.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class QuantoTermStructureVectorTests
{
    [Fact]
    public void QuantoTermStructureVector()
    {
        var file = VectorFile.Load("termstructures/yield/quantotermstructure");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            QuantoTermStructure q = new QuantoTermStructure(
                CurveOf(c.Input("dividend")),
                CurveOf(c.Input("riskFree")),
                CurveOf(c.Input("foreignRiskFree")),
                BlackVolOf(c.Input("underlyingVolatility"), context),
                c.Double("strike"),
                BlackVolOf(c.Input("exchangeRateVolatility"), context),
                c.Double("exchangeRateLevel"),
                c.Double("correlation")).EnableExtrapolation();
            Date[] dates = c.DateInputs("dates");
            check.Output(c, "referenceDate", q.ReferenceDate);
            check.Output(c, "calendarEmpty", q.Calendar is null);
            check.Output(c, "dayCounter", q.DayCounter.Name);
            check.Output(c, "maxDate", q.MaxDate);
            check.Output(c, "discount", dates.Select(d => (object?)q.Discount(d)).ToArray());
            check.Output(
                c, "zeroRate", dates.Select(d => (object?)q.ZeroRate(d, q.DayCounter, Compounding.Continuous).Rate).ToArray());
            check.Output(
                c,
                "forwardRate",
                dates.Select(d => (object?)q.ForwardRate(d, d + 91, q.DayCounter, Compounding.Simple).Rate).ToArray());
        }

        check.Complete();
    }
}
