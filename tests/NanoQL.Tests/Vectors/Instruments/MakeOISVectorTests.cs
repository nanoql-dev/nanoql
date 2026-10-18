// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The overnight-indexed swap builder vectors, <c>tests/vectors/instruments/makeois.json</c>: builds over
/// <see cref="MakeOIS"/>'s setters, at the par rate where no fixed rate is given, the default settlement days of the
/// presets among them, telescopic builds by the plan's telescopic rule, and QuantLib's refusals; with the built swap's
/// dates, schedules, terms and overnight conventions, and its results on the engine QuantLib's builder attaches, which
/// nanoql's swap doesn't hold. A rounding precision QuantLib refuses when it builds the swap is refused by
/// <see cref="MakeOIS.WithRoundingPrecision"/> already, so the build is refused either way.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MakeOISVectorTests
{
    [Fact]
    public void MakeOISVector()
    {
        var file = VectorFile.Load("instruments/makeois");
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

            Check(check, c, swap);
            var engine = SwapInputs.AttachedEngineOf(spec, swap.OvernightIndex);
            SwapInputs.CheckResults(check, c, () => engine(swap, context));
        }

        check.Complete();
    }

    /// <summary>Checks a built swap's dates, schedules, terms and overnight conventions.</summary>
    /// <param name="check">The file's comparison.</param>
    /// <param name="c">The case.</param>
    /// <param name="swap">The swap.</param>
    internal static void Check(VectorAssert check, VectorCase c, OvernightIndexedSwap swap)
    {
        check.Output(c, "startDate", swap.StartDate());
        check.Output(c, "maturityDate", swap.MaturityDate());
        check.Output(c, "fixedSchedule", swap.FixedSchedule.Dates);
        check.Output(c, "overnightSchedule", swap.OvernightSchedule.Dates);
        check.Output(c, "type", swap.Type);
        check.Output(c, "nominal", swap.Nominal());
        check.Output(c, "fixedRate", swap.FixedRate);
        check.Output(c, "spread", swap.Spread);
        check.Output(c, "fixedDayCount", swap.FixedDayCount.Name);
        check.Output(c, "paymentConvention", swap.PaymentConvention);
        check.Output(c, "paymentLag", swap.PaymentLag);
        check.Output(c, "averagingMethod", swap.AveragingMethod);
        check.Output(c, "lookbackDays", swap.LookbackDays);
        check.Output(c, "lockoutDays", swap.LockoutDays);
        check.Output(c, "applyObservationShift", swap.ApplyObservationShift);
    }
}
