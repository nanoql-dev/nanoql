// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.Instruments;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's overnight-indexed swap vectors, <c>tests/vectors/cxx/instruments/overnightindexedswap.json</c>:
/// <see cref="OvernightIndexedSwap.TelescopicValueDates"/> and <see cref="OvernightIndexedSwap.RoundingPrecision"/>,
/// which the wheel doesn't bind, and the fixed day counter and payment calendar of swaps built with QuantLib's empty
/// ones, which the wheel can't build; each swap is built for each output, so a refused construction is checked on
/// every one.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class OvernightIndexedSwapCxxVectorTests
{
    [Fact]
    public void OvernightIndexedSwapCxxVector()
    {
        var file = VectorFile.Load("cxx/instruments/overnightindexedswap");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement spec = c.Input("swap");
            OvernightIndexedSwap Swap() => SwapInputs.OvernightIndexedSwapOf(spec, context);
            check.Output(c, "telescopicValueDates", () => Swap().TelescopicValueDates);
            check.Output(c, "roundingPrecision", () => Swap().RoundingPrecision);
            check.Output(c, "fixedDayCount", () => Swap().FixedDayCount.Name);
            check.Output(c, "paymentCalendar", () => Swap().PaymentCalendar?.Name);
        }

        check.Complete();
    }
}
