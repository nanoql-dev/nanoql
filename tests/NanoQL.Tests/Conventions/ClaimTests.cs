// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Time;

namespace NanoQL.Tests.Conventions;

/// <summary>
/// The claims' contracts: a face-value accrual claim needs its reference security, an
/// <see cref="ArgumentNullException"/> naming it, where QuantLib accepts a null pointer and dereferences it when the
/// amount is read; a face-value claim's amount, <c>notional (1 - recoveryRate)</c>, reads no date and no context field.
/// </summary>
public class ClaimTests
{
    [Fact]
    public void AnAccrualClaimNeedsItsSecurityAndAFaceValueClaimNoDate()
    {
        ArgumentNullException e = Assert.Throws<ArgumentNullException>(() => new FaceValueAccrualClaim(null!));
        Assert.Equal("referenceSecurity", e.ParamName);

        var claim = new FaceValueClaim();
        var context = new EvaluationContext { EvaluationDate = new Date(15, Month.June, 2024) };
        Assert.Equal(600000.0, claim.Amount(null, 1e6, 0.4, context), 9);
        Assert.Equal(600000.0, claim.Amount(new Date(15, Month.June, 2030), 1e6, 0.4, context), 9);
    }
}
