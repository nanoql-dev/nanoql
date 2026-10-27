// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;
using NanoQL.Time;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The integral CDS engine vectors, <c>tests/vectors/pricingengines/credit/integralcdsengine.json</c>:
/// <see cref="IntegralCdsEngine"/>'s results on ten swaps, four credit curves, three integration steps, two recoveries
/// and two evaluation dates, with the settlement-date flows excluded, the wheel's default.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class IntegralCdsEngineVectorTests
{
    [Fact]
    public void IntegralCdsEngineVector()
    {
        var file = VectorFile.Load("pricingengines/credit/integralcdsengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            int[] step = c.Ints("step");
            CreditInputs.CheckResults(
                check,
                c,
                IntegralCdsEngine.Calculate(
                    CreditInputs.SwapOf(c.Input("swap"), context),
                    context,
                    new Period(step[0], (TimeUnit)step[1]),
                    CreditInputs.CurveOf(c.Input("credit"), context),
                    c.Double("recoveryRate"),
                    CreditInputs.DiscountOf(c.Input("discount")),
                    includeSettlementDateFlows: false));
        }

        check.Complete();
    }
}
