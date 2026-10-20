// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's ISDA CDS engine vectors, <c>tests/vectors/cxx/pricingengines/credit/isdacdsengine.json</c>:
/// <see cref="IsdaCdsEngine"/>'s results with the settlement-date flag unset, under the context's flags and each of
/// the engine's option sets, and its refusal of an option outside its enum.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class IsdaCdsEngineCxxVectorTests
{
    [Fact]
    public void IsdaCdsEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/pricingengines/credit/isdacdsengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            CreditDefaultSwapResults Price() => IsdaCdsEngine.Calculate(
                CreditInputs.SwapOf(c.Input("swap"), context),
                context,
                CreditInputs.CurveOf(c.Input("credit"), context),
                c.Double("recoveryRate"),
                CreditInputs.DiscountOf(c.Input("discount")),
                c.NullableBool("includeSettlementDateFlows"),
                (IsdaCdsEngineNumericalFix)c.Int("numericalFix"),
                (IsdaCdsEngineAccrualBias)c.Int("accrualBias"),
                (IsdaCdsEngineForwardsInCouponPeriod)c.Int("forwardsInCouponPeriod"));
            if (c.String("kind") == "option")
            {
                check.Output(c, "NPV", () => Price().NPV);
                continue;
            }

            CreditInputs.CheckResults(check, c, Price());
        }

        check.Complete();
    }
}
