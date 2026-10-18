// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The vanilla swap builder vectors, <c>tests/vectors/instruments/makevanillaswap.json</c>: builds over
/// <see cref="MakeVanillaSwap"/>'s setters, at the par rate where no fixed rate is given, the fixed leg's defaults for
/// the index's currency among them, and QuantLib's refusals; with the built swap's dates, schedules and terms, and its
/// results on the engine QuantLib's builder attaches, which nanoql's swap doesn't hold.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MakeVanillaSwapVectorTests
{
    [Fact]
    public void MakeVanillaSwapVector()
    {
        var file = VectorFile.Load("instruments/makevanillaswap");
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

            Check(check, c, swap);
            var engine = SwapInputs.AttachedEngineOf(spec, swap.IborIndex);
            SwapInputs.CheckResults(check, c, () => engine(swap, context));
        }

        check.Complete();
    }

    /// <summary>Checks a built swap's dates, schedules and terms.</summary>
    /// <param name="check">The file's comparison.</param>
    /// <param name="c">The case.</param>
    /// <param name="swap">The swap.</param>
    internal static void Check(VectorAssert check, VectorCase c, FixedVsFloatingSwap swap)
    {
        check.Output(c, "startDate", swap.StartDate());
        check.Output(c, "maturityDate", swap.MaturityDate());
        check.Output(c, "fixedSchedule", swap.FixedSchedule.Dates);
        check.Output(c, "floatingSchedule", swap.FloatingSchedule.Dates);
        check.Output(c, "type", swap.Type);
        check.Output(c, "nominal", swap.Nominal());
        check.Output(c, "fixedRate", swap.FixedRate);
        check.Output(c, "spread", swap.Spread);
        check.Output(c, "fixedDayCount", swap.FixedDayCount.Name);
        check.Output(c, "floatingDayCount", swap.FloatingDayCount?.Name);
        check.Output(c, "paymentConvention", swap.PaymentConvention);
    }
}
