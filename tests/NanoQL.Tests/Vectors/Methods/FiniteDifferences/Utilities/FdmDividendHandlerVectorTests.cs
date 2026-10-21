// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Methods;
using NanoQL.Tests.Harness;
using NanoQL.Time;
using static NanoQL.Tests.Vectors.EquityInputs;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The FdmDividendHandler vectors, <c>tests/vectors/methods/finitedifferences/utilities/fdmdividendhandler.json</c>:
/// <see cref="FdmDividendHandler"/>'s dividend times, dates and amounts, and its
/// <see cref="FdmDividendHandler.ApplyTo"/> at each dividend's time and at a time with none, over four schedules on
/// one- and two-dimensional meshers.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmDividendHandlerVectorTests
{
    [Fact]
    public void FdmDividendHandlerVector()
    {
        var file = VectorFile.Load("methods/finitedifferences/utilities/fdmdividendhandler");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            var handler = new FdmDividendHandler(
                DividendsOf(c.Input("dividends")),
                MesherOf(c.Input("mesher"), context),
                new Date(c.Int("referenceDate")),
                CashFlowInputs.DayCounterOf(c.Input("dayCounter")),
                c.Int("direction"),
                context);
            double[] a = Doubles(c.Input("array"));
            check.Output(c, "dividendTimes", handler.DividendTimes.ToArray());
            check.Output(c, "dividendDates", handler.DividendDates.Select(d => (object?)d.SerialNumber).ToArray());
            check.Output(c, "dividends", handler.Dividends.ToArray());
            check.Output(c, "applyTo", Doubles(c.Input("times")).Select(t => (object?)Applied(handler, a, t)).ToArray());
        }

        check.Complete();
    }
}
