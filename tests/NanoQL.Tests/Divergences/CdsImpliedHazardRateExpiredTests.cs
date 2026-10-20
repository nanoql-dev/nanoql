// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Tests.Conventions;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The implied hazard rate of an expired credit default swap. QuantLib's <c>impliedHazardRate</c> calls the engine
/// directly, without the expiry check (<c>creditdefaultswap.cpp:317-336</c>), and the engine finds every coupon
/// occurred, so the objective is the target less a constant: for <see cref="CreditFixture"/>'s expired swap, under
/// either model, a zero target returns the solver's guess, 0.016898148148148148, and a target of 1 fails to bracket
/// ("unable to bracket root in 100 function evaluations …") (QuantLib's defect, R8). nanoql refuses an expired swap
/// with a <see cref="NanoQLException"/>, "instrument expired", QuantLib's text in <c>Swaption::impliedVolatility</c>.
/// </summary>
public class CdsImpliedHazardRateExpiredTests
{
    [Fact]
    public void AZeroTargetIsRefused()
    {
        // QuantLib's: 0.016898148148148148 under both models, its guess
        AssertRefused(0.0);
    }

    [Fact]
    public void ANonZeroTargetIsRefused()
    {
        // QuantLib's: "unable to bracket root in 100 function evaluations" under both models
        AssertRefused(1.0);
    }

    private static void AssertRefused(double target)
    {
        EvaluationContext context = CreditFixture.Context();
        CreditDefaultSwap cds = CreditFixture.Swap(true, 0.01);
        foreach (CreditDefaultSwapPricingModel model in
                 new[] { CreditDefaultSwapPricingModel.Midpoint, CreditDefaultSwapPricingModel.ISDA })
        {
            NanoQLException e = Assert.Throws<NanoQLException>(
                () => cds.ImpliedHazardRate(
                    target, CreditFixture.Discount(), new Actual365Fixed(), context, 0.4, 1e-8, model));
            Assert.Equal("instrument expired", e.Message);
        }
    }
}
