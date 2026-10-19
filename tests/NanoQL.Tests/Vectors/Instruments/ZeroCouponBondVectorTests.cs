// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Instruments;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The zero-coupon bond vectors, <c>tests/vectors/instruments/bonds/zerocouponbond.json</c>: 12 bonds and the outputs
/// every bond file shares (<see cref="BondInputs"/>).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class ZeroCouponBondVectorTests
{
    [Fact]
    public void ZeroCouponBondVector()
    {
        var file = VectorFile.Load("instruments/bonds/zerocouponbond");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            var bond = (ZeroCouponBond)BondInputs.BondOf(c.Input("bond"), context);
            BondInputs.CheckStatic(check, c, bond, context);
            BondInputs.CheckDated(check, c, bond, context);
            BondInputs.CheckEngine(check, c, bond, context);
        }

        check.Complete();
    }
}
