// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Tests.Harness;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The tree engine's vectors, <c>tests/vectors/experimental/callablebonds/treecallablebondengine.json</c>: eight
/// callable bonds on fourteen engines each, by time steps, on a time grid and on a lattice built from the model's tree on
/// that grid, with Hull-White and Vasicek models, and the engine's refusals (<see cref="CallableBondInputs"/>).
/// </summary>
[Trait("Oracle", "QuantLib")]
public class TreeCallableBondEngineVectorTests
{
    [Fact]
    public void TreeCallableBondEngineVector()
    {
        var file = VectorFile.Load("experimental/callablebonds/treecallablebondengine");
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
