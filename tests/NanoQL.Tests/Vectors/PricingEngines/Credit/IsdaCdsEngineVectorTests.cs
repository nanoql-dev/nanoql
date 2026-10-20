// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The ISDA CDS engine vectors, <c>tests/vectors/pricingengines/credit/isdacdsengine.json</c>:
/// <see cref="IsdaCdsEngine"/>'s results on ten swaps, flat and noded discount and credit curves, four option sets and
/// the settlement-date flag given, and its refusals of the swaps and curves it isn't specified for.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class IsdaCdsEngineVectorTests
{
    [Fact]
    public void IsdaCdsEngineVector()
    {
        var file = VectorFile.Load("pricingengines/credit/isdacdsengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            CreditDefaultSwapResults? results = null;
            Exception? error = null;
            try
            {
                results = IsdaCdsEngine.Calculate(
                    CreditInputs.SwapOf(c.Input("swap"), context),
                    context,
                    CreditInputs.CurveOf(c.Input("credit"), context),
                    c.Double("recoveryRate"),
                    CreditInputs.DiscountOf(c.Input("discount")),
                    c.Bool("includeSettlementDateFlows"),
                    (IsdaCdsEngineNumericalFix)c.Int("numericalFix"),
                    (IsdaCdsEngineAccrualBias)c.Int("accrualBias"),
                    (IsdaCdsEngineForwardsInCouponPeriod)c.Int("forwardsInCouponPeriod"));
            }
            catch (Exception e) when (e is ArgumentException or NanoQLException)
            {
                error = e;
            }

            if (results is not null)
            {
                CreditInputs.CheckResults(check, c, results);
                continue;
            }

            foreach (string name in new[]
                     {
                         "NPV", "couponLegNPV", "defaultLegNPV", "upfrontNPV", "accrualRebateNPV", "fairSpread",
                         "fairUpfront", "couponLegBPS", "upfrontBPS",
                     })
            {
                check.Output(c, name, () => throw error!);
            }
        }

        check.Complete();
    }
}
