// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Methods;
using NanoQL.Numerics;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The Dupire operator vectors, <c>tests/vectors/experimental/finitedifferences/fdmdupire1dop.json</c>: on three strike
/// meshers with a local volatility per point, at three time steps, the size, apply, each direction's apply and
/// splitting solutions, and the preconditioner; apply_mixed left out, QuantLib's returning its input.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmDupire1dOpVectorTests
{
    [Fact]
    public void FdmDupire1dOpVector()
    {
        var file = VectorFile.Load("experimental/finitedifferences/fdmdupire1dop");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            FdmMesherComposite mesher = MesherOf(c.Input("mesher"), c.Context());
            var op = new FdmDupire1dOp(mesher, new Vector(c.Doubles("localVolatility")));
            ShortRateFdInputs.CheckOperator(check, c, op, 1);
        }

        check.Complete();
    }
}
