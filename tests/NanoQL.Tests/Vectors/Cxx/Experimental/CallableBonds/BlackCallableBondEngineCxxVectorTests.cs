// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's Black engine vectors, <c>tests/vectors/cxx/experimental/callablebonds/blackcallablebondengine.json</c>:
/// the fixed-rate engine on constant volatility structures by reference date and by settlement days, and the
/// zero-coupon engine, on bonds with one call or put date: the NPV and the settlement value.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class BlackCallableBondEngineCxxVectorTests
{
    [Fact]
    public void BlackCallableBondEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/callablebonds/blackcallablebondengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            CallableBond bond = CallableBondInputs.BondOf(c.Input("bond"), context);
            CallableBondResults results = CallableBondInputs.EngineOf(c.Input("engine"))(bond, context, 0.0);
            check.Output(c, "NPV", results.NPV);
            check.Output(c, "settlementValue", results.SettlementValue);
        }

        check.Complete();
    }
}
