// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Indexes;
using NanoQL.Instruments;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's swap-index vectors, <c>tests/vectors/cxx/indexes/swapindex.json</c>:
/// <see cref="SwapIndex.ExogenousDiscount"/> and the base's <see cref="SwapIndex.UnderlyingSwap"/>, which the wheel
/// doesn't bind: the swap's dates, fixed rate and schedules, the vanilla swap of daily coupons for an
/// <see cref="OvernightIndexedSwapIndex"/>, whose own underlying swap hides it.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class SwapIndexCxxVectorTests
{
    [Fact]
    public void SwapIndexCxxVector()
    {
        var file = VectorFile.Load("cxx/indexes/swapindex");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            SwapIndex index = SwapIndexInputs.SwapIndexOf(c.Input("index"));
            Lazy<VanillaSwap>[] swaps = [.. c.DateInputs("dates").Select(d => new Lazy<VanillaSwap>(
                () => index.UnderlyingSwap(d, context)))];
            check.Output(c, "exogenousDiscount", index.ExogenousDiscount);
            check.Output(c, "startDate", Each(swaps, s => s.StartDate()));
            check.Output(c, "maturityDate", Each(swaps, s => s.MaturityDate()));
            check.Output(c, "fixedRate", Each(swaps, s => s.FixedRate));
            check.Output(c, "fixedSchedule", Each(swaps, s => s.FixedSchedule.Dates.Select(x => (object)x)));
            check.Output(c, "floatingScheduleSize", Each(swaps, s => s.FloatingSchedule.Size));
            check.Output(
                c, "floatingScheduleFront", Each(swaps, s => s.FloatingSchedule.Dates.Take(5).Select(x => (object)x)));
            check.Output(
                c, "floatingScheduleBack", Each(swaps, s => s.FloatingSchedule.Dates.TakeLast(5).Select(x => (object)x)));
            check.Output(c, "fixedLegSize", Each(swaps, s => s.FixedLeg.Length));
            check.Output(c, "floatingLegSize", Each(swaps, s => s.FloatingLeg.Length));
        }

        check.Complete();
    }

    private static Func<object?>[] Each<TOut>(Lazy<VanillaSwap>[] swaps, Func<VanillaSwap, TOut> f) =>
        swaps.Select(s => (Func<object?>)(() => f(s.Value))).ToArray();
}
