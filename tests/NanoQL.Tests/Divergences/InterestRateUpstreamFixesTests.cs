// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Time;

namespace NanoQL.Tests.Divergences;

/// <summary>
/// The fix QuantLib's master made to <see cref="InterestRate.ImpliedRate(double, DayCounter, Compounding, Frequency,
/// double)"/> after the pin, which nanoql takes (R8, the upstream sweep): #2898 (<c>588c2dd5b</c>) refuses the mixed
/// compoundings, <see cref="Compounding.SimpleThenCompounded"/> and <see cref="Compounding.CompoundedThenSimple"/>,
/// before its unit compound factor's branch. QuantLib 1.44 refuses them only in its other branch
/// (<c>interestrate.cpp:127-129</c>), so at a compound factor of 1 it returns a zero rate on a rule it can't apply to
/// any other factor. #2898's null-rate checks have no counterpart: a nanoql rate is never null, so its two null-rate
/// cases aren't ported.
/// </summary>
public class InterestRateUpstreamFixesTests
{
    [Fact]
    public void TestImpliedRateRejectsMixedCompoundingAtUnity()
    {
        // Upstream: QuantLib's master's case for #2898 (interestrates.cpp's testImpliedRateRejectsMixedCompoundingAtUnity)
        foreach (Compounding comp in new[] { Compounding.SimpleThenCompounded, Compounding.CompoundedThenSimple })
        {
            Assert.Contains(
                "not supported",
                Assert.Throws<NanoQLException>(
                    () => InterestRate.ImpliedRate(1.0, new Actual365Fixed(), comp, Frequency.Semiannual, 1.0)).Message);
        }
    }
}
