// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Tests.Conventions;
using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The conventional spread where the engine leaves the fair spread null. QuantLib's <c>conventionalSpread</c> returns
/// the results' fair spread unchecked (<c>creditdefaultswap.cpp:422</c>), so it returns <c>Null&lt;Real&gt;()</c>,
/// 3.4028234663852886e+38, as a spread under either model: for <see cref="CreditFixture"/>'s expired swap, and for its
/// live swap at a zero running spread, whose root is a zero hazard rate and no coupon leg. nanoql's results are
/// <c>double?</c>, so QuantLib's sentinel has no form here (the forced-departure rule): nanoql refuses with
/// <see cref="NanoQLException"/>, "instrument expired" for the expired swap and QuantLib's own "fair spread not
/// available" for the live one.
/// </summary>
public class CdsConventionalSpreadNullTests
{
    [Fact]
    public void AnExpiredSwapIsRefused()
    {
        // QuantLib's: 3.4028234663852886e+38 under both models
        AssertRefused(CreditFixture.Swap(true, 0.01), "instrument expired");
    }

    [Fact]
    public void AZeroRunningSpreadIsRefused()
    {
        // QuantLib's: 3.4028234663852886e+38 under both models
        AssertRefused(CreditFixture.Swap(false, 0.0), "fair spread not available");
    }

    private static void AssertRefused(CreditDefaultSwap cds, string text)
    {
        EvaluationContext context = CreditFixture.Context();
        foreach (CreditDefaultSwapPricingModel model in
                 new[] { CreditDefaultSwapPricingModel.Midpoint, CreditDefaultSwapPricingModel.ISDA })
        {
            NanoQLException e = Assert.Throws<NanoQLException>(
                () => cds.ConventionalSpread(0.4, CreditFixture.Discount(), new Actual365Fixed(), context, model));
            Assert.Equal(text, e.Message);
        }
    }
}
