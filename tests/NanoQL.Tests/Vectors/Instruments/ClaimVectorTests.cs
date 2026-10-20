// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The claim vectors, <c>tests/vectors/instruments/claim.json</c>: <see cref="FaceValueClaim"/>'s and
/// <see cref="FaceValueAccrualClaim"/>'s amounts, the latter on two bonds through their lives.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ClaimVectorTests
{
    [Fact]
    public void ClaimVector()
    {
        var file = VectorFile.Load("instruments/claim");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            Claim claim = c.String("kind") == "face"
                ? new FaceValueClaim()
                : new FaceValueAccrualClaim(BondInputs.BondOf(c.Input("bond"), context));
            check.Output(
                c,
                "amount",
                () => claim.Amount(c.NullableDateInput("defaultDate"), c.Double("notional"), c.Double("recoveryRate"), context));
        }

        check.Complete();
    }
}
