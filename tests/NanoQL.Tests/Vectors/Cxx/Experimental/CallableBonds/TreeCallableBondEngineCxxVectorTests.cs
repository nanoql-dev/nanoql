// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.ArrayVectorTests;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The C++ harness's tree engine vectors, <c>tests/vectors/cxx/experimental/callablebonds/treecallablebondengine.json</c>:
/// the fixed-rate and zero-coupon tree engines at four spreads, which QuantLib's NPVSpreadHelper writes into the
/// bond's arguments and nanoql's engines take as their last parameter, their value and settlement value at each.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class TreeCallableBondEngineCxxVectorTests
{
    [Fact]
    public void TreeCallableBondEngineCxxVector()
    {
        var file = VectorFile.Load("cxx/experimental/callablebonds/treecallablebondengine");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            EvaluationContext context = c.Context();
            CallableBond bond = CallableBondInputs.BondOf(c.Input("bond"), context);
            Func<CallableBond, EvaluationContext, double, CallableBondResults> engine =
                CallableBondInputs.EngineOf(c.Input("engine"));
            CallableBondResults[] results = [.. Numbers(c.Input("spreads")).Select(s => engine(bond, context, s))];
            check.Output(c, "value", results.Select(r => r.NPV));
            check.Output(c, "settlementValue", results.Select(r => r.SettlementValue));
        }

        check.Complete();
    }
}
