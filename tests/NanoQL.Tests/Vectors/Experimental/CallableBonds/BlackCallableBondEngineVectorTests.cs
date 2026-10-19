// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Black engine's vectors, <c>tests/vectors/experimental/callablebonds/blackcallablebondengine.json</c>: fixed-rate
/// and zero-coupon bonds with one call or put date, at three forward yield volatilities, and the engine's refusals of
/// two callability dates and of none (<see cref="CallableBondInputs"/>).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BlackCallableBondEngineVectorTests
{
    [Fact]
    public void BlackCallableBondEngineVector()
    {
        var file = VectorFile.Load("experimental/callablebonds/blackcallablebondengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            CallableBond bond = CallableBondInputs.BondOf(c.Input("bond"), context);
            CallableBondInputs.CheckEngine(check, c, bond, context, CallableBondInputs.EngineOf(c.Input("engine")));
        }

        check.Complete();
    }
}
