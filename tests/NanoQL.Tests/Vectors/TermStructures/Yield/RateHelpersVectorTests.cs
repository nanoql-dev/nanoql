// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using System.Text.Json;
using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The rate-helper vectors, <c>tests/vectors/termstructures/yield/ratehelpers.json</c>:
/// <see cref="DepositRateHelper"/>'s three forms and <see cref="SwapRateHelper"/>'s two, each built in its case's
/// context: whether it builds, its five dates, and for a swap helper its swap's start and maturity dates and fixed
/// rate, and its spread.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class RateHelpersVectorTests
{
    [Fact]
    public void RateHelpersVector()
    {
        var file = VectorFile.Load("termstructures/yield/ratehelpers");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            JsonElement spec = c.Input("helper");
            BootstrapHelper<YieldTermStructure>? helper = null;
            check.Output(c, "built", () =>
            {
                helper = RateHelperInputs.HelperOf(spec, context);
                return true;
            });
            if (helper is null)
            {
                continue;
            }

            RateHelperInputs.CheckDates(check, c, helper);
            if (helper is SwapRateHelper swap)
            {
                check.Output(c, "swapStartDate", swap.Swap.StartDate());
                check.Output(c, "swapMaturityDate", swap.Swap.MaturityDate());
                check.Output(c, "swapFixedRate", swap.Swap.FixedRate);
                check.Output(c, "spread", swap.Spread);
            }
        }

        check.Complete();
    }
}
