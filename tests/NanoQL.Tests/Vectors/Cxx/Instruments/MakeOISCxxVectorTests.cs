// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's overnight-indexed swap builder vectors, <c>tests/vectors/cxx/instruments/makeois.json</c>:
/// <see cref="MakeOIS.WithSettlementCalendar"/>, which the wheel's builder doesn't reach, with the start and maturity
/// dates, schedules and fixed rate of each build.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MakeOISCxxVectorTests
{
    [Fact]
    public void MakeOISCxxVector()
    {
        var file = VectorFile.Load("cxx/instruments/makeois");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement spec = c.Input("build");
            OvernightIndexedSwap? swap = null;
            check.Output(c, "built", () =>
            {
                swap = SwapInputs.MakeOISOf(spec).ToOvernightIndexedSwap(context);
                return true;
            });
            if (swap is null)
            {
                continue;
            }

            check.Output(c, "startDate", swap.StartDate());
            check.Output(c, "maturityDate", swap.MaturityDate());
            check.Output(c, "fixedSchedule", swap.FixedSchedule.Dates);
            check.Output(c, "overnightSchedule", swap.OvernightSchedule.Dates);
            check.Output(c, "fixedRate", swap.FixedRate);
        }

        check.Complete();
    }
}
