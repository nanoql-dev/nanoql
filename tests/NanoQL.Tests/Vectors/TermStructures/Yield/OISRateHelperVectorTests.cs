// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.TermStructures;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The OIS rate-helper vectors, <c>tests/vectors/termstructures/yield/oisratehelper.json</c>:
/// <see cref="OISRateHelper"/>'s two forms, each built in its case's context (<see cref="RateHelperInputs"/>), with
/// whether it builds, its five dates and its swap's start and maturity dates and payment dates; then curves
/// bootstrapped on OIS helpers (<see cref="BootstrapInputs"/>), with their nodes, discounts and the helpers' quote
/// errors, one of them built in a context later than its helpers', which re-dates them.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class OISRateHelperVectorTests
{
    [Fact]
    public void OISRateHelperVector()
    {
        var file = VectorFile.Load("termstructures/yield/oisratehelper");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            if (c.Inputs.TryGetProperty("kind", out _))
            {
                // a curve built in a context later than its helpers' re-dates them
                EvaluationContext helpersContext = c.String("kind") == "moved"
                    ? context with { EvaluationDate = c.DateInput("helpersEvaluationDate") }
                    : context;
                BootstrapInputs.CheckCurves(
                    check, c, RateHelperInputs.HelpersOf(c.Input("helpers"), helpersContext), context);
                continue;
            }

            OISRateHelper? helper = null;
            check.Output(c, "built", () =>
            {
                helper = (OISRateHelper)RateHelperInputs.HelperOf(c.Input("helper"), context);
                return true;
            });
            if (helper is null)
            {
                continue;
            }

            RateHelperInputs.CheckDates(check, c, helper);
            check.Output(c, "swapStartDate", helper.Swap.StartDate());
            check.Output(c, "swapMaturityDate", helper.Swap.MaturityDate());
            check.Output(c, "fixedPaymentDates", helper.Swap.FixedLeg.Select(cf => (object?)cf.Date).ToArray());
            check.Output(c, "overnightPaymentDates", helper.Swap.OvernightLeg.Select(cf => (object?)cf.Date).ToArray());
        }

        check.Complete();
    }
}
