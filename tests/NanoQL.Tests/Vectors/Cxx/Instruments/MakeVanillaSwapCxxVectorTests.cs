// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's vanilla swap builder vectors, <c>tests/vectors/cxx/instruments/makevanillaswap.json</c>:
/// <see cref="MakeVanillaSwap.WithSettlementCalendar"/>, which the wheel's builder doesn't reach, with the start and
/// maturity dates, schedules and fixed rate of each build.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MakeVanillaSwapCxxVectorTests
{
    [Fact]
    public void MakeVanillaSwapCxxVector()
    {
        var file = VectorFile.Load("cxx/instruments/makevanillaswap");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement spec = c.Input("build");
            VanillaSwap? swap = null;
            check.Output(c, "built", () =>
            {
                swap = SwapInputs.MakeVanillaSwapOf(spec).ToVanillaSwap(context);
                return true;
            });
            if (swap is null)
            {
                continue;
            }

            check.Output(c, "startDate", swap.StartDate());
            check.Output(c, "maturityDate", swap.MaturityDate());
            check.Output(c, "fixedSchedule", swap.FixedSchedule.Dates);
            check.Output(c, "floatingSchedule", swap.FloatingSchedule.Dates);
            check.Output(c, "fixedRate", swap.FixedRate);
        }

        check.Complete();
    }
}
