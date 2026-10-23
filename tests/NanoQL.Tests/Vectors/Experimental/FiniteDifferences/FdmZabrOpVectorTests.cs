// SPDX-FileCopyrightText: 2026 The nanoql Authors
// SPDX-License-Identifier: Apache-2.0

using NanoQL.Experimental;
using NanoQL.Methods;
using NanoQL.Tests.Harness;
using static NanoQL.Tests.Vectors.FdInputs;

namespace NanoQL.Tests.Vectors;

/// <summary>
/// The ZABR operator vectors, <c>tests/vectors/experimental/finitedifferences/fdmzabrop.json</c>: three parameter sets
/// on two forward-by-volatility meshers, at three time steps, the size, apply, apply_mixed, each direction's apply and
/// splitting solutions, and the preconditioner.
/// </summary>
[Trait("Oracle", "QuantLib")]
public class FdmZabrOpVectorTests
{
    [Fact]
    public void FdmZabrOpVector()
    {
        var file = VectorFile.Load("experimental/finitedifferences/fdmzabrop");
        var check = new VectorAssert(file);
        foreach (VectorCase c in file.Cases)
        {
            FdmMesherComposite mesher = MesherOf(c.Input("mesher"), c.Context());
            var op = new FdmZabrOp(mesher, c.Double("beta"), c.Double("nu"), c.Double("rho"), c.Double("gamma"));
            ShortRateFdInputs.CheckOperator(check, c, op, 2);
        }

        check.Complete();
    }
}
