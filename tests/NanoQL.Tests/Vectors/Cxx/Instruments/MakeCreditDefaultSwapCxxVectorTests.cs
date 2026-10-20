// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's CDS builder vectors, <c>tests/vectors/cxx/instruments/makecds.json</c>:
/// <see cref="MakeCreditDefaultSwap"/> from a schedule and with the setters the wheel doesn't bind, the swaps'
/// inspectors, and its refusals.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class MakeCreditDefaultSwapCxxVectorTests
{
    [Fact]
    public void MakeCreditDefaultSwapCxxVector()
    {
        var file = VectorFile.Load("cxx/instruments/makecds");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            if (c.Expected.TryGetProperty("construction", out _))
            {
                check.Output(
                    c,
                    "construction",
                    () => MakeCreditDefaultSwapVectorTests.BuilderOf(c.Input("builder"), context).ToCreditDefaultSwap(context) is null ? 1 : 0);
                continue;
            }

            CreditInputs.CheckInspectors(
                check,
                c,
                MakeCreditDefaultSwapVectorTests.BuilderOf(c.Input("builder"), context).ToCreditDefaultSwap(context),
                context);
        }

        check.Complete();
    }
}
