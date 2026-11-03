// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.PricingEngines;
using NanoQL.Tests.Conventions;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// An expired credit default swap's accrual-rebate NPV. QuantLib's <c>setupExpired</c> zeroes seven results and leaves
/// the accrual rebate's NPV unset (<c>creditdefaultswap.cpp:215-220</c>), so it reads an uninitialized member: on the
/// wheel, for <see cref="CreditFixture"/>'s expired swap priced by the mid-point engine, 7.565977700541159e-307 in one
/// fresh process and 1.9925540477164e-311 in another (QuantLib's defect). nanoql's expired results are all 0, the
/// accrual rebate's NPV included.
/// </summary>
public class CdsExpiredAccrualRebateTests
{
    [Fact]
    public void TheAccrualRebateNpvOfAnExpiredSwapIsZero()
    {
        EvaluationContext context = CreditFixture.Context();
        CreditDefaultSwap cds = CreditFixture.Swap(true, 0.01);

        CreditDefaultSwapResults results = MidPointCdsEngine.Calculate(
            cds, context, CreditFixture.Probability(), 0.4, CreditFixture.Discount());

        Assert.True(cds.IsExpired(context));
        Assert.Equal(0.0, results.AccrualRebateNPV);
        Assert.Equal(0.0, results.NPV);
        Assert.Equal(0.0, results.FairSpread);
        Assert.Equal(0.0, results.UpfrontBPS);
    }
}
